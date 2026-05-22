using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace Equativ.RoaringBitmaps;

/// <summary>
/// Fused bitwise op + popcount for BitmapContainer's fixed-size (1024 ulong = 8 KiB) bitmaps.
///
/// The non-fused pattern does a scalar op pass, then re-reads the full bitmap to count bits.
/// Fusing keeps the just-written vectors hot in registers/L1 and removes the second pass.
///
/// NEON path: loads 4 Vector128 (8 ulongs) per iteration, runs the op, popcounts each byte
/// lane (vcnt), sums in u8 (max 4 * 8 = 32, safe), then pairwise-widens into a u16 accumulator.
/// 512 vectors / 4 = 128 iterations - far below the ~2k iterations where u16 lanes would saturate.
/// Final reduction widens u16 -> u32 once before the horizontal sum.
/// </summary>
internal static class BitmapOps
{
    private const int BitmapLength = 1024;
    private const int Vectors = BitmapLength / 2; // Vector128<byte> count = 512

    internal interface IBinaryOp
    {
        static abstract Vector128<byte> ApplyVec(Vector128<byte> a, Vector128<byte> b);
        static abstract ulong ApplyScalar(ulong a, ulong b);
    }

    internal readonly struct AndOp : IBinaryOp
    {
        public static Vector128<byte> ApplyVec(Vector128<byte> a, Vector128<byte> b) => AdvSimd.And(a, b);
        public static ulong ApplyScalar(ulong a, ulong b) => a & b;
    }

    internal readonly struct OrOp : IBinaryOp
    {
        public static Vector128<byte> ApplyVec(Vector128<byte> a, Vector128<byte> b) => AdvSimd.Or(a, b);
        public static ulong ApplyScalar(ulong a, ulong b) => a | b;
    }

    internal readonly struct XorOp : IBinaryOp
    {
        public static Vector128<byte> ApplyVec(Vector128<byte> a, Vector128<byte> b) => AdvSimd.Xor(a, b);
        public static ulong ApplyScalar(ulong a, ulong b) => a ^ b;
    }

    internal readonly struct AndNotOp : IBinaryOp
    {
        // BitwiseClear(a, b) = a AND NOT b
        public static Vector128<byte> ApplyVec(Vector128<byte> a, Vector128<byte> b) => AdvSimd.BitwiseClear(a, b);
        public static ulong ApplyScalar(ulong a, ulong b) => a & ~b;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int FusedBinary<TOp>(ulong[] first, ulong[] second) where TOp : struct, IBinaryOp
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            ref Vector128<byte> p = ref Unsafe.As<ulong, Vector128<byte>>(ref MemoryMarshal.GetArrayDataReference(first));
            ref Vector128<byte> q = ref Unsafe.As<ulong, Vector128<byte>>(ref MemoryMarshal.GetArrayDataReference(second));

            Vector128<ushort> acc = Vector128<ushort>.Zero;

            for (int i = 0; i < Vectors; i += 4)
            {
                Vector128<byte> r0 = TOp.ApplyVec(Unsafe.Add(ref p, i),     Unsafe.Add(ref q, i));
                Vector128<byte> r1 = TOp.ApplyVec(Unsafe.Add(ref p, i + 1), Unsafe.Add(ref q, i + 1));
                Vector128<byte> r2 = TOp.ApplyVec(Unsafe.Add(ref p, i + 2), Unsafe.Add(ref q, i + 2));
                Vector128<byte> r3 = TOp.ApplyVec(Unsafe.Add(ref p, i + 3), Unsafe.Add(ref q, i + 3));

                Unsafe.Add(ref p, i)     = r0;
                Unsafe.Add(ref p, i + 1) = r1;
                Unsafe.Add(ref p, i + 2) = r2;
                Unsafe.Add(ref p, i + 3) = r3;

                Vector128<byte> sum = AdvSimd.Add(
                    AdvSimd.Add(AdvSimd.PopCount(r0), AdvSimd.PopCount(r1)),
                    AdvSimd.Add(AdvSimd.PopCount(r2), AdvSimd.PopCount(r3)));

                acc = AdvSimd.AddPairwiseWideningAndAdd(acc, sum);
            }

            return (int)Vector128.Sum(AdvSimd.AddPairwiseWidening(acc));
        }

        // Scalar fused fallback. The op part will be auto-vectorized by the JIT on x64 (vpand/vpor/vpxor);
        // POPCNT stays scalar but has 1/cycle throughput. Either way the single pass is faster than
        // op-then-popcount because the result stays hot in L1.
        int cnt = 0;
        for (int k = 0; k < BitmapLength; k++)
        {
            ulong v = TOp.ApplyScalar(first[k], second[k]);
            first[k] = v;
            cnt += BitOperations.PopCount(v);
        }
        return cnt;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int FusedNot(ulong[] data)
    {
        if (AdvSimd.Arm64.IsSupported)
        {
            ref Vector128<byte> p = ref Unsafe.As<ulong, Vector128<byte>>(ref MemoryMarshal.GetArrayDataReference(data));

            Vector128<ushort> acc = Vector128<ushort>.Zero;

            for (int i = 0; i < Vectors; i += 4)
            {
                Vector128<byte> r0 = AdvSimd.Not(Unsafe.Add(ref p, i));
                Vector128<byte> r1 = AdvSimd.Not(Unsafe.Add(ref p, i + 1));
                Vector128<byte> r2 = AdvSimd.Not(Unsafe.Add(ref p, i + 2));
                Vector128<byte> r3 = AdvSimd.Not(Unsafe.Add(ref p, i + 3));

                Unsafe.Add(ref p, i)     = r0;
                Unsafe.Add(ref p, i + 1) = r1;
                Unsafe.Add(ref p, i + 2) = r2;
                Unsafe.Add(ref p, i + 3) = r3;

                Vector128<byte> sum = AdvSimd.Add(
                    AdvSimd.Add(AdvSimd.PopCount(r0), AdvSimd.PopCount(r1)),
                    AdvSimd.Add(AdvSimd.PopCount(r2), AdvSimd.PopCount(r3)));

                acc = AdvSimd.AddPairwiseWideningAndAdd(acc, sum);
            }

            return (int)Vector128.Sum(AdvSimd.AddPairwiseWidening(acc));
        }

        int cnt = 0;
        for (int k = 0; k < BitmapLength; k++)
        {
            ulong v = ~data[k];
            data[k] = v;
            cnt += BitOperations.PopCount(v);
        }
        return cnt;
    }
}
