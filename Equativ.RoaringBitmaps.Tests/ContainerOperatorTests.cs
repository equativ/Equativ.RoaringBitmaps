using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Equativ.RoaringBitmaps.Tests;

public class ContainerOperatorTests
{
    private static List<int> ToList(Container c)
    {
        var list = new List<int>();
        c.EnumerateFill(list, 0);
        return list;
    }

    [Fact]
    public void Or_ArrayContainers_ThroughBaseOperator()
    {
        Container a = ArrayContainer.Create(new ushort[] {1, 3});
        Container b = ArrayContainer.Create(new ushort[] {3, 5});

        Container result = a | b;

        Assert.Equal(new[] {1,3,5}, ToList(result));
    }

    [Fact]
    public void Xor_MixedContainers_ThroughBaseOperator()
    {
        Container a = ArrayContainer.Create(new ushort[] {1, 2});
        Container b = BitmapContainer.Create(new ushort[] {2, 4});

        Container result = a ^ b;

        Assert.Equal(new[] {1,4}, ToList(result));
    }

    [Fact]
    public void And_MixedContainers_ThroughBaseOperator()
    {
        Container a = ArrayContainer.Create(new ushort[] {10, 11, 12});
        Container b = BitmapContainer.Create(new ushort[] {11, 13});

        Container result = a & b;

        Assert.Equal(new[] {11}, ToList(result));
    }

    [Fact]
    public void Xor_LargeDisjointArrayContainers_ProducesBitmapContainer()
    {
        var a = ArrayContainer.Create(Enumerable.Range(0, 3000).Select(i => (ushort) i).ToArray());
        var b = ArrayContainer.Create(Enumerable.Range(3000, 3000).Select(i => (ushort) i).ToArray());

        Container result = a ^ b;

        Assert.Equal(6000, result.Cardinality);
        Assert.IsType<BitmapContainer>(result);
    }

    [Fact]
    public void Xor_LargeOverlappingArrayContainers_ProducesArrayContainer()
    {
        var a = ArrayContainer.Create(Enumerable.Range(0, 3000).Select(i => (ushort) i).ToArray());
        var b = ArrayContainer.Create(Enumerable.Range(100, 3000).Select(i => (ushort) i).ToArray());

        Container result = a ^ b;

        Assert.Equal(200, result.Cardinality);
        Assert.IsType<ArrayContainer>(result);
    }

    [Fact]
    public void AndNot_MixedContainers()
    {
        Container a = ArrayContainer.Create(new ushort[] {8, 9, 10});
        Container b = BitmapContainer.Create(new ushort[] {9});

        Container result = Container.AndNot(a, b);

        Assert.Equal(new[] {8,10}, ToList(result));
    }

    [Fact]
    public void OrInPlace_UnsharedContainers_AreUpdatedInPlace()
    {
        Container array = ArrayContainer.Create(new ushort[] {1, 3});
        Container bitmap = BitmapContainer.Create(Enumerable.Range(0, 5000).Select(i => (ushort) i).ToArray());

        Assert.Same(array, Container.OrInPlace(array, ArrayContainer.Create(new ushort[] {2, 3, 4})));
        Assert.Same(bitmap, Container.OrInPlace(bitmap, ArrayContainer.Create(new ushort[] {5000})));
        Assert.Same(bitmap, Container.OrInPlace(bitmap, BitmapContainer.Create(Enumerable.Range(5000, 5000).Select(i => (ushort) i).ToArray())));

        Assert.Equal(new[] {1, 2, 3, 4}, ToList(array));
        Assert.Equal(Enumerable.Range(0, 10000), ToList(bitmap));
    }

    [Fact]
    public void OrInPlace_SharedContainer_IsLeftUntouched()
    {
        Container a = ArrayContainer.Create(new ushort[] {1, 3}).MarkShared();

        Container result = Container.OrInPlace(a, ArrayContainer.Create(new ushort[] {2}));

        Assert.NotSame(a, result);
        Assert.Equal(new[] {1, 3}, ToList(a));
        Assert.Equal(new[] {1, 2, 3}, ToList(result));
    }
}
