using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Equativ.RoaringBitmaps;

// Based on Harley-Seal
internal static class PopcntAvx512
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Popcnt(ReadOnlySpan<byte> bytes)
    {
        ulong total = Popcnt(MemoryMarshal.Cast<byte, Vector512<uint>>(bytes), bytes.Length / 64);

        // Handle remaining bytes
        for (int i = bytes.Length - bytes.Length % 64; i < bytes.Length; i++)
        {
            total += (ulong)BitOperations.PopCount(bytes[i]);
        }

        return total;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<ushort> Popcnt(Vector512<byte> v)
    {
        Vector512<byte> m1 = Vector512.Create((byte)0x55);
        Vector512<byte> m2 = Vector512.Create((byte)0x33);
        Vector512<byte> m4 = Vector512.Create((byte)0x0F);

        Vector512<byte> t1 = Avx512BW.Subtract(v, Avx512F.And(Avx512BW.ShiftRightLogical(v.AsUInt16(), 1).AsByte(), m1));
        Vector512<byte> t2 = Avx512BW.Add(Avx512F.And(t1, m2), Avx512F.And(Avx512BW.ShiftRightLogical(t1.AsUInt16(), 2).AsByte(), m2));
        Vector512<byte> t3 = Avx512F.And(Avx512BW.Add(t2, Avx512BW.ShiftRightLogical(t2.AsUInt16(), 4).AsByte()), m4);
        return Avx512BW.SumAbsoluteDifferences(t3, Vector512<byte>.Zero);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CSA(out Vector512<uint> h, out Vector512<uint> l, Vector512<uint> a, Vector512<uint> b, Vector512<uint> c)
    {
        l = Avx512F.TernaryLogic(c, b, a, 0x96);
        h = Avx512F.TernaryLogic(c, b, a, 0xe8);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Popcnt(ReadOnlySpan<Vector512<uint>> data, int size)
    {
        Vector512<ulong> total = Vector512<ulong>.Zero;
        Vector512<uint> ones = Vector512<uint>.Zero;
        Vector512<uint> twos = Vector512<uint>.Zero;
        Vector512<uint> fours = Vector512<uint>.Zero;
        Vector512<uint> eights = Vector512<uint>.Zero;
        Vector512<uint> sixteens = Vector512<uint>.Zero;
        Vector512<uint> twosA, twosB, foursA, foursB, eightsA, eightsB;

        int limit = size - size % 16;
        int i = 0;

        for (; i < limit; i += 16)
        {
            CSA(out twosA, out ones, ones, data[i + 0], data[i + 1]);
            CSA(out twosB, out ones, ones, data[i + 2], data[i + 3]);
            CSA(out foursA, out twos, twos, twosA, twosB);
            CSA(out twosA, out ones, ones, data[i + 4], data[i + 5]);
            CSA(out twosB, out ones, ones, data[i + 6], data[i + 7]);
            CSA(out foursB, out twos, twos, twosA, twosB);
            CSA(out eightsA, out fours, fours, foursA, foursB);
            CSA(out twosA, out ones, ones, data[i + 8], data[i + 9]);
            CSA(out twosB, out ones, ones, data[i + 10], data[i + 11]);
            CSA(out foursA, out twos, twos, twosA, twosB);
            CSA(out twosA, out ones, ones, data[i + 12], data[i + 13]);
            CSA(out twosB, out ones, ones, data[i + 14], data[i + 15]);
            CSA(out foursB, out twos, twos, twosA, twosB);
            CSA(out eightsB, out fours, fours, foursA, foursB);
            CSA(out sixteens, out eights, eights, eightsA, eightsB);

            total = Avx512F.Add(total, Popcnt(sixteens.AsByte()).AsUInt64());
        }

        total = Avx512F.ShiftLeftLogical(total, 4);
        total = Avx512F.Add(total, Avx512F.ShiftLeftLogical(Popcnt(eights.AsByte()).AsUInt64(), 3));
        total = Avx512F.Add(total, Avx512F.ShiftLeftLogical(Popcnt(fours.AsByte()).AsUInt64(), 2));
        total = Avx512F.Add(total, Avx512F.ShiftLeftLogical(Popcnt(twos.AsByte()).AsUInt64(), 1));
        total = Avx512F.Add(total, Popcnt(ones.AsByte()).AsUInt64());

        for (; i < size; i++)
        {
            total = Avx512F.Add(total, Popcnt(data[i].AsByte()).AsUInt64());
        }

        return SimdSumEpu64(total);
    }

    private static ulong SimdSumEpu64(Vector512<ulong> v)
    {
        Vector256<ulong> sum256 = Avx2.Add(v.GetLower(), v.GetUpper());
        Vector128<ulong> sum128 = Sse2.Add(sum256.GetLower(), sum256.GetUpper());
        return sum128.GetElement(0) + sum128.GetElement(1);
    }
}