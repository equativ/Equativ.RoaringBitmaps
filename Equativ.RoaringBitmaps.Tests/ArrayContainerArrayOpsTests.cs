using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Equativ.RoaringBitmaps.Tests;

public class ArrayContainerArrayOpsTests
{
    private static bool BitSet(ulong[] bitmap, int value)
    {
        int index = value >> 6;
        ulong mask = 1UL << value;
        return (bitmap[index] & mask) != 0;
    }

    [Fact]
    public void OrArray_EmptyBitmap_SetsAllBits()
    {
        var ac = ArrayContainer.Create(new ushort[] {1, 63, 64, 500});
        var bitmap = new ulong[1024];

        int added = ac.OrArray(bitmap);

        Assert.Equal(ac.Cardinality, added);
        Assert.True(BitSet(bitmap, 1));
        Assert.True(BitSet(bitmap, 63));
        Assert.True(BitSet(bitmap, 64));
        Assert.True(BitSet(bitmap, 500));
}

    [Fact]
    public void OrArray_WithExistingBits_AddsOnlyMissing()
    {
        var ac = ArrayContainer.Create(new ushort[] {1, 200, 500});
        var bitmap = new ulong[1024];
        // pre-set bit 1 and 200
        bitmap[1 >> 6] |= 1UL << 1;
        bitmap[200 >> 6] |= 1UL << 200;

        int added = ac.OrArray(bitmap);

        Assert.Equal(1, added); // only value 500 was added
        Assert.True(BitSet(bitmap, 1));
        Assert.True(BitSet(bitmap, 200));
        Assert.True(BitSet(bitmap, 500));
    }

    [Fact]
    public void XorArray_EmptyBitmap_TogglesBits()
    {
        var ac = ArrayContainer.Create(new ushort[] {2, 100});
        var bitmap = new ulong[1024];

        int delta = ac.XorArray(bitmap);

        Assert.Equal(ac.Cardinality, delta);
        Assert.True(BitSet(bitmap, 2));
        Assert.True(BitSet(bitmap, 100));
    }

    [Fact]
    public void XorArray_WithExistingBits_TogglesOff()
    {
        var ac = ArrayContainer.Create(new ushort[] {2, 100});
        var bitmap = new ulong[1024];
        bitmap[2 >> 6] |= 1UL << 2; // set bit 2

        int delta = ac.XorArray(bitmap);

        Assert.Equal(0, delta); // one added, one removed
        Assert.False(BitSet(bitmap, 2));
        Assert.True(BitSet(bitmap, 100));
    }

    [Fact]
    public void AndNotArray_NoBitsSet_NoChange()
    {
        var ac = ArrayContainer.Create(new ushort[] {3, 30});
        var bitmap = new ulong[1024];

        int delta = ac.AndNotArray(bitmap);

        Assert.Equal(0, delta);
        Assert.False(BitSet(bitmap, 3));
        Assert.False(BitSet(bitmap, 30));
    }

    [Theory]
    [InlineData(new ushort[] {1, 3, 5}, new ushort[] {2, 3, 4}, false, new ushort[] {1, 2, 3, 4, 5})]
    [InlineData(new ushort[] {1, 3, 5}, new ushort[] {2, 3, 4}, true, new ushort[] {1, 2, 4, 5})]
    [InlineData(new ushort[] {1, 2, 3}, new ushort[] {1, 2, 3}, false, new ushort[] {1, 2, 3})]
    [InlineData(new ushort[] {1, 2, 3}, new ushort[] {1, 2, 3}, true, new ushort[0])]
    [InlineData(new ushort[] {5, 6}, new ushort[] {1, 2, 3}, false, new ushort[] {1, 2, 3, 5, 6})]
    [InlineData(new ushort[] {1, 2}, new ushort[] {5, 6, 7}, true, new ushort[] {1, 2, 5, 6, 7})]
    [InlineData(new ushort[0], new ushort[] {1, 2}, false, new ushort[] {1, 2})]
    [InlineData(new ushort[] {1, 2}, new ushort[0], true, new ushort[] {1, 2})]
    public void MergeArraysInPlace(ushort[] set1, ushort[] set2, bool xor, ushort[] expected)
    {
        var buffer = new ushort[set1.Length + set2.Length];
        set1.CopyTo(buffer, 0);

        var length = Utils.MergeArraysInPlace(buffer, set1.Length, set2, set2.Length, xor);

        Assert.Equal(expected, buffer.Take(length).ToArray());
    }

    [Fact]
    public void MergeArraysInPlace_Random_MatchesLinq()
    {
        var random = new Random(42);
        for (var iteration = 0; iteration < 500; iteration++)
        {
            var set1 = Enumerable.Range(0, random.Next(0, 300)).Select(_ => (ushort) random.Next(0, 400)).Distinct().Order().ToArray();
            var set2 = Enumerable.Range(0, random.Next(0, 300)).Select(_ => (ushort) random.Next(0, 400)).Distinct().Order().ToArray();
            var union = new ushort[set1.Length + set2.Length];
            var xor = new ushort[set1.Length + set2.Length];
            set1.CopyTo(union, 0);
            set1.CopyTo(xor, 0);

            var unionLength = Utils.MergeArraysInPlace(union, set1.Length, set2, set2.Length, false);
            var xorLength = Utils.MergeArraysInPlace(xor, set1.Length, set2, set2.Length, true);

            Assert.Equal(set1.Union(set2).Order(), union.Take(unionLength));
            Assert.Equal(set1.Except(set2).Union(set2.Except(set1)).Order(), xor.Take(xorLength));
        }
    }

    [Fact]
    public void AndNotArray_RemovesExistingBits()
    {
        var ac = ArrayContainer.Create(new ushort[] {3, 30});
        var bitmap = new ulong[1024];
        bitmap[3 >> 6] |= 1UL << 3;
        bitmap[30 >> 6] |= 1UL << 30;
        bitmap[40 >> 6] |= 1UL << 40;

        int delta = ac.AndNotArray(bitmap);

        Assert.Equal(-2, delta);
        Assert.False(BitSet(bitmap, 3));
        Assert.False(BitSet(bitmap, 30));
        Assert.True(BitSet(bitmap, 40));
    }
}
