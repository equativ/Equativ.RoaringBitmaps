using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;

namespace Equativ.RoaringBitmaps;

internal static class PopcntNeon
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Popcnt(ReadOnlySpan<ulong> data)
    {
        ref Vector128<byte> p = ref Unsafe.As<ulong, Vector128<byte>>(ref MemoryMarshal.GetReference(data));
        int vectors = data.Length / 2;

        // Accumulate byte popcounts pairwise-widened into u16 lanes. Per-lane growth is at most
        // 32 per outer iteration (4 vectors * max 8 bits/byte), so u16 saturation is only a concern
        // after ~2k iterations - far beyond any realistic call site (BitmapContainer is 128 iters).
        Vector128<ushort> acc = Vector128<ushort>.Zero;

        int unrolled = vectors & ~3;
        ref Vector128<byte> unrolledEnd = ref Unsafe.Add(ref p, unrolled);

        while (Unsafe.IsAddressLessThan(ref p, ref unrolledEnd))
        {
            Vector128<byte> c0 = AdvSimd.PopCount(p);
            Vector128<byte> c1 = AdvSimd.PopCount(Unsafe.Add(ref p, 1));
            Vector128<byte> c2 = AdvSimd.PopCount(Unsafe.Add(ref p, 2));
            Vector128<byte> c3 = AdvSimd.PopCount(Unsafe.Add(ref p, 3));

            // Sum stays in u8 (max 4 * 8 = 32), then widen-add into u16 lanes.
            Vector128<byte> sum = AdvSimd.Add(AdvSimd.Add(c0, c1), AdvSimd.Add(c2, c3));
            acc = AdvSimd.AddPairwiseWideningAndAdd(acc, sum);

            p = ref Unsafe.Add(ref p, 4);
        }

        // 1-3 remaining full vectors.
        ref Vector128<byte> end = ref Unsafe.Add(ref p, vectors - unrolled);
        while (Unsafe.IsAddressLessThan(ref p, ref end))
        {
            acc = AdvSimd.AddPairwiseWideningAndAdd(acc, AdvSimd.PopCount(p));
            p = ref Unsafe.Add(ref p, 1);
        }

        // Widen u16 -> u32 before the horizontal sum: a full bitmap has up to 65536 set bits
        // total, which would overflow a u16 reduction.
        Vector128<uint> wider = AdvSimd.AddPairwiseWidening(acc);
        ulong total = Vector128.Sum(wider);

        // Tail ulong when data.Length is odd.
        if ((data.Length & 1) != 0)
        {
            total += (ulong)BitOperations.PopCount(data[data.Length - 1]);
        }

        return total;
    }
}
