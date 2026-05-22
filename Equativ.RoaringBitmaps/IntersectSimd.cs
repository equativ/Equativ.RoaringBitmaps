using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Equativ.RoaringBitmaps;

/// <summary>
/// SIMD intersection for two sorted, unique-element ushort arrays.
///
/// For each pair of 8-ushort blocks we compare v1 against all 8 cyclic rotations of v2,
/// OR'ing the comparison masks to identify which lanes of v1 are present anywhere in v2.
/// The 8-bit lane mask is then used to index a precomputed shuffle table that compresses
/// matching lanes to the front of a vector for a single 128-bit store.
///
/// Cross-platform: Vector128.Shuffle lowers to PSHUFB on x86 and TBL on AArch64, both
/// single-cycle throughput on modern uarchs. The "advance side with smaller max" trick
/// guarantees we process each element at most once.
/// </summary>
internal static class IntersectSimd
{
    // Cyclic ushort rotate: [a,b,c,d,e,f,g,h] -> [b,c,d,e,f,g,h,a]
    private static readonly Vector128<ushort> RotateLeft1 = Vector128.Create((ushort)1, 2, 3, 4, 5, 6, 7, 0);

    // Per-lane bit weights for emulating MOVMSK on ushort lanes.
    private static readonly Vector128<ushort> BitWeights = Vector128.Create((ushort)1, 2, 4, 8, 16, 32, 64, 128);

    // 256 entries: for each 8-bit match mask, byte-shuffle indices that compress the
    // matching ushort lanes to the front. Unused tail bytes are 0xFF (PSHUFB/TBL zero).
    private static readonly Vector128<byte>[] CompressTable = BuildCompressTable();

    private static Vector128<byte>[] BuildCompressTable()
    {
        var table = new Vector128<byte>[256];
        for (int m = 0; m < 256; m++)
        {
            Span<byte> idx = stackalloc byte[16];
            idx.Fill(0xFF);
            int pos = 0;
            for (int i = 0; i < 8; i++)
            {
                if ((m & (1 << i)) != 0)
                {
                    idx[pos * 2]     = (byte)(i * 2);
                    idx[pos * 2 + 1] = (byte)(i * 2 + 1);
                    pos++;
                }
            }
            table[m] = Vector128.Create(idx);
        }
        return table;
    }

    /// <summary>
    /// Intersect two sorted, unique-element ushort spans. Returns the number of matches
    /// written to <paramref name="buffer"/>. Buffer must have at least min(set1.Length, set2.Length) slots.
    /// </summary>
    public static int Intersect(ReadOnlySpan<ushort> set1, ReadOnlySpan<ushort> set2, ushort[] buffer)
    {
        int len1 = set1.Length;
        int len2 = set2.Length;
        if (len1 == 0 || len2 == 0)
        {
            return 0;
        }

        int k1 = 0, k2 = 0, pos = 0;

        if (Vector128.IsHardwareAccelerated && len1 >= 8 && len2 >= 8)
        {
            ref ushort r1 = ref MemoryMarshal.GetReference(set1);
            ref ushort r2 = ref MemoryMarshal.GetReference(set2);
            ref ushort rOut = ref buffer[0];

            int last1 = len1 - 8;
            int last2 = len2 - 8;

            while (k1 <= last1 && k2 <= last2)
            {
                Vector128<ushort> v1 = Vector128.LoadUnsafe(ref r1, (nuint)k1);
                Vector128<ushort> v2 = Vector128.LoadUnsafe(ref r2, (nuint)k2);

                Vector128<ushort> rot = v2;
                Vector128<ushort> matchMask = Vector128.Equals(v1, rot);
                rot = Vector128.Shuffle(rot, RotateLeft1); matchMask |= Vector128.Equals(v1, rot);
                rot = Vector128.Shuffle(rot, RotateLeft1); matchMask |= Vector128.Equals(v1, rot);
                rot = Vector128.Shuffle(rot, RotateLeft1); matchMask |= Vector128.Equals(v1, rot);
                rot = Vector128.Shuffle(rot, RotateLeft1); matchMask |= Vector128.Equals(v1, rot);
                rot = Vector128.Shuffle(rot, RotateLeft1); matchMask |= Vector128.Equals(v1, rot);
                rot = Vector128.Shuffle(rot, RotateLeft1); matchMask |= Vector128.Equals(v1, rot);
                rot = Vector128.Shuffle(rot, RotateLeft1); matchMask |= Vector128.Equals(v1, rot);

                // Lane mask (each lane 0 or 0xFFFF) -> 8-bit mask
                int mask = Vector128.Sum(matchMask & BitWeights);

                // Compress matching lanes to the front and store the full 128 bits.
                // Lanes beyond popcount(mask) are zero from the shuffle and get overwritten
                // by the next iteration (or are past the returned count - see bounds note below).
                Vector128<ushort> compressed = Vector128.Shuffle(v1.AsByte(), CompressTable[mask]).AsUInt16();
                compressed.StoreUnsafe(ref rOut, (nuint)pos);
                pos += BitOperations.PopCount((uint)mask);

                ushort max1 = Unsafe.Add(ref r1, k1 + 7);
                ushort max2 = Unsafe.Add(ref r2, k2 + 7);
                if (max1 <= max2) k1 += 8;
                if (max2 <= max1) k2 += 8;
            }
        }

        return pos + ScalarTail(set1, k1, set2, k2, buffer, pos);
    }

    // Standard 2-by-2 scalar intersect, picking up from (k1, k2) and writing into buffer at bufferPos.
    private static int ScalarTail(ReadOnlySpan<ushort> set1, int k1, ReadOnlySpan<ushort> set2, int k2, ushort[] buffer, int bufferPos)
    {
        int len1 = set1.Length, len2 = set2.Length;
        if (k1 >= len1 || k2 >= len2)
        {
            return 0;
        }

        int pos = bufferPos;
        ushort s1 = set1[k1];
        ushort s2 = set2[k2];

        while (true)
        {
            int v1 = s1, v2 = s2;
            if (v2 < v1)
            {
                do
                {
                    ++k2;
                    if (k2 == len2) return pos - bufferPos;
                    s2 = set2[k2];
                    v2 = s2;
                } while (v2 < v1);
            }
            if (v1 < v2)
            {
                do
                {
                    ++k1;
                    if (k1 == len1) return pos - bufferPos;
                    s1 = set1[k1];
                    v1 = s1;
                } while (v1 < v2);
            }
            else
            {
                buffer[pos++] = s1;
                ++k1;
                if (k1 == len1) break;
                ++k2;
                if (k2 == len2) break;
                s1 = set1[k1];
                s2 = set2[k2];
            }
        }
        return pos - bufferPos;
    }
}
