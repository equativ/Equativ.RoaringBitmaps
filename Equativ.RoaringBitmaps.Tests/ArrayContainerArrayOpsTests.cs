using System;
using System.Collections.Generic;
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
