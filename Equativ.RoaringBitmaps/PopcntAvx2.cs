using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace Equativ.RoaringBitmaps;

// Based on Harley-Seal
internal static class PopcntAvx2
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ulong Popcnt(ReadOnlySpan<ulong> longs)
    {
        ulong total = Popcnt(MemoryMarshal.Cast<ulong, Vector256<long>>(longs), longs.Length / 4);

        // Handle remaining bytes
        for (int i = longs.Length - longs.Length % 4; i < longs.Length; i++)
        {
            total += (ulong)BitOperations.PopCount(longs[i]);
        }

        return total;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<long> Popcnt(Vector256<byte> v)
    {
        Vector256<byte> m1 = Vector256.Create((byte)0x55);
        Vector256<byte> m2 = Vector256.Create((byte)0x33);
        Vector256<byte> m4 = Vector256.Create((byte)0x0F);

        Vector256<byte> t1 = Avx2.Subtract(v, Avx2.And(Avx2.ShiftRightLogical(v.AsUInt16(), 1).AsByte(), m1));
        Vector256<byte> t2 = Avx2.Add(Avx2.And(t1, m2), Avx2.And(Avx2.ShiftRightLogical(t1.AsUInt16(), 2).AsByte(), m2));
        Vector256<byte> t3 = Avx2.And(Avx2.Add(t2, Avx2.ShiftRightLogical(t2.AsUInt16(), 4).AsByte()), m4);
        return Avx2.SumAbsoluteDifferences(t3, Vector256<byte>.Zero).AsInt64();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CSA(out Vector256<long> h, out Vector256<long> l, Vector256<long> a, Vector256<long> b, Vector256<long> c)
    {
        Vector256<long> u = Avx2.Xor(a, b);
        h = Avx2.Or(Avx2.And(a, b), Avx2.And(u, c));
        l = Avx2.Xor(u, c);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Popcnt(ReadOnlySpan<Vector256<long>> data, int size)
    {
        Vector256<long> total = Vector256<long>.Zero;
        Vector256<long> ones = Vector256<long>.Zero;
        Vector256<long> twos = Vector256<long>.Zero;
        Vector256<long> fours = Vector256<long>.Zero;
        Vector256<long> eights = Vector256<long>.Zero;
        Vector256<long> sixteens = Vector256<long>.Zero;
        Vector256<long> twosA, twosB, foursA, foursB, eightsA, eightsB;

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

            total = Avx2.Add(total, Popcnt(sixteens.AsByte()));
        }

        total = Avx2.ShiftLeftLogical(total, 4);
        total = Avx2.Add(total, Avx2.ShiftLeftLogical(Popcnt(eights.AsByte()), 3));
        total = Avx2.Add(total, Avx2.ShiftLeftLogical(Popcnt(fours.AsByte()), 2));
        total = Avx2.Add(total, Avx2.ShiftLeftLogical(Popcnt(twos.AsByte()), 1));
        total = Avx2.Add(total, Popcnt(ones.AsByte()));

        for (; i < size; i++)
        {
            total = Avx2.Add(total, Popcnt(data[i].AsByte()));
        }

        return (ulong)total.GetElement(0) +
               (ulong)total.GetElement(1) +
               (ulong)total.GetElement(2) +
               (ulong)total.GetElement(3);
    }
}