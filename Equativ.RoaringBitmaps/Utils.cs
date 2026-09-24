using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Equativ.RoaringBitmaps;

internal static class Utils
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Popcnt(ulong[] longs)
    {
        if (AdvSimd.IsSupported)
        {
            return (int)PopcntNeon.Popcnt(longs.AsSpan());
        }
        if (Avx2.IsSupported)
        {
            return (int)PopcntAvx2.Popcnt(longs.AsSpan());
        }
        // AVX512 Support needs proper testing before being enabled
        // if (Avx512BW.IsSupported)
        // {
        //     return (int)PopcntAvx512.Popcnt(longs.AsSpan());
        // }
        
        return Popcnt64.Popcnt(longs);
    }
    
    /// <summary>
    /// Input and output may be the same array with overlapping ranges (Array.Copy behaves like memmove)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ArrayCopy(ushort[] input, int iStart, ushort[] output, int oStart, int length)
    {
        Array.Copy(input, iStart, output, oStart, length);
    }

    /// <summary>
    /// Merges set2 into set1 in place, as a union (xor = false) or a symmetric difference (xor = true).
    /// set1 must have room for length1 + length2 values. Returns the resulting length.
    /// </summary>
    public static int MergeArraysInPlace(ushort[] set1, int length1, ushort[] set2, int length2, bool xor)
    {
        // Merging from the back never overwrites an unread value of set1: the write index k stays strictly
        // ahead of the read index i as long as values of set2 remain to be merged.
        var i = length1 - 1;
        var j = length2 - 1;
        var k = length1 + length2 - 1;
        while (i >= 0 && j >= 0)
        {
            var s1 = set1[i];
            var s2 = set2[j];
            if (s1 > s2)
            {
                set1[k--] = s1;
                i--;
            }
            else if (s1 < s2)
            {
                set1[k--] = s2;
                j--;
            }
            else
            {
                if (!xor)
                {
                    set1[k--] = s1;
                }
                i--;
                j--;
            }
        }
        while (j >= 0)
        {
            set1[k--] = set2[j--];
        }
        // set1[0..i] is already in place. The merged tail sits at [k + 1, length1 + length2) and is moved down
        // to close the gap left by duplicates (or by xor cancellations).
        var tailLength = length1 + length2 - 1 - k;
        if (k > i)
        {
            ArrayCopy(set1, k + 1, set1, i + 1, tailLength);
        }
        return i + 1 + tailLength;
    }

    public static int UnionArrays(ushort[] set1, int length1, ushort[] set2, int length2, ushort[] buffer)
    {
        var pos = 0;
        int k1 = 0, k2 = 0;
        if (0 == length2)
        {
            ArrayCopy(set1, 0, buffer, 0, length1);
            return length1;
        }
        if (0 == length1)
        {
            ArrayCopy(set2, 0, buffer, 0, length2);
            return length2;
        }
        var s1 = set1[k1];
        var s2 = set2[k2];
        while (true)
        {
            int v1 = s1;
            int v2 = s2;
            if (v1 < v2)
            {
                buffer[pos++] = s1;
                ++k1;
                if (k1 >= length1)
                {
                    ArrayCopy(set2, k2, buffer, pos, length2 - k2);
                    return pos + length2 - k2;
                }
                s1 = set1[k1];
            }
            else if (v1 == v2)
            {
                buffer[pos++] = s1;
                ++k1;
                ++k2;
                if (k1 >= length1)
                {
                    ArrayCopy(set2, k2, buffer, pos, length2 - k2);
                    return pos + length2 - k2;
                }
                if (k2 >= length2)
                {
                    ArrayCopy(set1, k1, buffer, pos, length1 - k1);
                    return pos + length1 - k1;
                }
                s1 = set1[k1];
                s2 = set2[k2];
            }
            else // if (set1[k1]>set2[k2])
            {
                buffer[pos++] = s2;
                ++k2;
                if (k2 >= length2)
                {
                    ArrayCopy(set1, k1, buffer, pos, length1 - k1);
                    return pos + length1 - k1;
                }
                s2 = set2[k2];
            }
        }
    }

    public static int DifferenceArrays(ushort[] set1, int length1, ushort[] set2, int length2, ushort[] buffer)
    {
        var pos = 0;
        int k1 = 0, k2 = 0;
        if (0 == length2)
        {
            ArrayCopy(set1, 0, buffer, 0, length1);
            return length1;
        }
        if (0 == length1)
        {
            return 0;
        }
        var s1 = set1[k1];
        var s2 = set2[k2];
        while (true)
        {
            if (s1 < s2)
            {
                buffer[pos++] = s1;
                ++k1;
                if (k1 >= length1)
                {
                    break;
                }
                s1 = set1[k1];
            }
            else if (s1 == s2)
            {
                ++k1;
                ++k2;
                if (k1 >= length1)
                {
                    break;
                }
                if (k2 >= length2)
                {
                    ArrayCopy(set1, k1, buffer, pos, length1 - k1);
                    return pos + length1 - k1;
                }
                s1 = set1[k1];
                s2 = set2[k2];
            }
            else // if (val1>val2)
            {
                ++k2;
                if (k2 >= length2)
                {
                    ArrayCopy(set1, k1, buffer, pos, length1 - k1);
                    return pos + length1 - k1;
                }
                s2 = set2[k2];
            }
        }
        return pos;
    }

    public static int IntersectArrays(ReadOnlySpan<ushort> set1, ReadOnlySpan<ushort> set2, ushort[] buffer)
    {
        if (set1.Length << 6 < set2.Length)
        {
            return OneSidedGallopingIntersect2By2(set1, set2, buffer);
        }
        if (set2.Length << 6 < set1.Length)
        {
            return OneSidedGallopingIntersect2By2(set2, set1, buffer);
        }
        return LocalIntersect2By2(set1, set2, buffer);
    }

    private static int LocalIntersect2By2(ReadOnlySpan<ushort> set1, ReadOnlySpan<ushort> set2, ushort[] buffer)
    {
        if (0 == set1.Length || 0 == set2.Length)
        {
            return 0;
        }
        var k1 = 0;
        var k2 = 0;
        var pos = 0;
        var s1 = set1[k1];
        var s2 = set2[k2];

        while (true)
        {
            int v1 = s1;
            int v2 = s2;
            if (v2 < v1)
            {
                do
                {
                    ++k2;
                    if (k2 == set2.Length)
                    {
                        return pos;
                    }
                    s2 = set2[k2];
                    v2 = s2;
                } while (v2 < v1);
            }
            if (v1 < v2)
            {
                do
                {
                    ++k1;
                    if (k1 == set1.Length)
                    {
                        return pos;
                    }
                    s1 = set1[k1];
                    v1 = s1;
                } while (v1 < v2);
            }
            else // (set2[k2] == set1[k1])
            {
                buffer[pos++] = s1;
                ++k1;
                if (k1 == set1.Length)
                {
                    break;
                }
                ++k2;
                if (k2 == set2.Length)
                {
                    break;
                }
                s1 = set1[k1];
                s2 = set2[k2];
            }
        }
        return pos;
    }

    private static int OneSidedGallopingIntersect2By2(ReadOnlySpan<ushort> smallSet, ReadOnlySpan<ushort> largeSet, ushort[] buffer)
    {
        if (0 == smallSet.Length)
        {
            return 0;
        }
        var k1 = 0;
        var k2 = 0;
        var pos = 0;
        var s1 = largeSet[k1];
        var s2 = smallSet[k2];
        while (true)
        {
            if (s1 < s2)
            {
                k1 = AdvanceUntil(largeSet, k1, s2);
                if (k1 == largeSet.Length)
                {
                    break;
                }
                s1 = largeSet[k1];
            }
            if (s2 < s1)
            {
                ++k2;
                if (k2 == smallSet.Length)
                {
                    break;
                }
                s2 = smallSet[k2];
            }
            else // (set2[k2] == set1[k1])
            {
                buffer[pos++] = s2;
                ++k2;
                if (k2 == smallSet.Length)
                {
                    break;
                }
                s2 = smallSet[k2];
                k1 = AdvanceUntil(largeSet, k1, s2);
                if (k1 == largeSet.Length)
                {
                    break;
                }
                s1 = largeSet[k1];
            }
        }
        return pos;
    }

    /// <summary>
    /// Find the smallest integer larger than pos such that array[pos]&gt;= min. otherwise return length
    /// -> The first line is BinarySearch with pos + 1, the second line is the bitwise complement if the value can't be found
    /// </summary>
    public static int AdvanceUntil(ReadOnlySpan<ushort> span, int pos, ushort min)
    {
        var start = pos + 1; // check the next one
        if (start >= span.Length || span[start] >= min) // the simple cases
        {
            return start;
        }
        var result = span.Slice(start).BinarySearch(min);
        return (result < 0 ? ~result : result) + start;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort HighBits(int value)
    {
        return (ushort) (value >> 16);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort LowBits(int value)
    {
        return (ushort) (value & 0xFFFF);
    }

    public static int XorArrays(ushort[] set1, int length1, ushort[] set2, int length2, ushort[] buffer)
    {
        var pos = 0;
        int k1 = 0, k2 = 0;
        if (0 == length2)
        {
            ArrayCopy(set1, 0, buffer, 0, length1);
            return length1;
        }
        if (0 == length1)
        {
            ArrayCopy(set2, 0, buffer, 0, length2);
            return length2;
        }
        var s1 = set1[k1];
        var s2 = set2[k2];
        while (true)
        {
            if (s1 < s2)
            {
                buffer[pos++] = s1;
                ++k1;
                if (k1 >= length1)
                {
                    ArrayCopy(set2, k2, buffer, pos, length2 - k2);
                    return pos + length2 - k2;
                }
                s1 = set1[k1];
            }
            else if (s1 == s2)
            {
                ++k1;
                ++k2;
                if (k1 >= length1)
                {
                    ArrayCopy(set2, k2, buffer, pos, length2 - k2);
                    return pos + length2 - k2;
                }
                if (k2 >= length2)
                {
                    ArrayCopy(set1, k1, buffer, pos, length1 - k1);
                    return pos + length1 - k1;
                }
                s1 = set1[k1];
                s2 = set2[k2];
            }
            else // if (val1>val2)
            {
                buffer[pos++] = s2;
                ++k2;
                if (k2 >= length2)
                {
                    ArrayCopy(set1, k1, buffer, pos, length1 - k1);
                    return pos + length1 - k1;
                }
                s2 = set2[k2];
            }
        }
    }
}