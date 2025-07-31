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
    public void AndNot_MixedContainers()
    {
        Container a = ArrayContainer.Create(new ushort[] {8, 9, 10});
        Container b = BitmapContainer.Create(new ushort[] {9});

        Container result = Container.AndNot(a, b);

        Assert.Equal(new[] {8,10}, ToList(result));
    }
}
