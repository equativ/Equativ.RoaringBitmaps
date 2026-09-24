using Xunit;

namespace Equativ.RoaringBitmaps.Tests;

public class RoaringBitmapInPlaceTests
{
    private static IEnumerable<int> Range(int start, int count) => Enumerable.Range(start, count);

    /// <summary>Deterministic, re-enumerable pseudo random values (Create sorts the array it is given in place)</summary>
    private static IEnumerable<int> RandomValues(int seed, int count)
    {
        var random = new Random(seed);
        for (var i = 0; i < count; i++)
        {
            yield return random.Next(0, 1 << 20);
        }
    }

    /// <summary>
    /// Operand pairs covering every container combination: array/array, array/bitmap, bitmap/array, bitmap/bitmap,
    /// an array union overflowing into a bitmap, identical containers (xor cancels out), keys on one side only,
    /// empty operands and random mixes.
    /// </summary>
    public static TheoryData<IEnumerable<int>, IEnumerable<int>> OperandPairs => new()
    {
        { Range(1000, 200), Range(1100, 400) },
        { Range(1000, 200), Range(1100, 10000) },
        { Range(1000, 10000), Range(1100, 400) },
        { Range(1000, 10000), Range(4000, 10000) },
        { Range(0, 5000), Range(1000, 10000) },
        { Range(0, 3000), Range(3000, 3000) },
        { Range(1000, 10000), Range(1000, 10000) },
        { Range(0, 100), Range(1 << 16, 100) },
        { Range(0, 100), Enumerable.Empty<int>() },
        { Enumerable.Empty<int>(), Range(0, 100) },
        { RandomValues(1, 50000), RandomValues(2, 50000) },
        { RandomValues(3, 5000).Concat(Range(1 << 17, 10000)), RandomValues(4, 5000).Concat(Range((1 << 17) + 5000, 10000)) },
    };

    public static TheoryData<string, Action<RoaringBitmap, RoaringBitmap>> InPlaceOperations => new()
    {
        { "Or", (x, y) => x.OrInPlace(y) },
        { "And", (x, y) => x.AndInPlace(y) },
        { "Xor", (x, y) => x.XorInPlace(y) },
        { "AndNot", (x, y) => x.AndNotInPlace(y) },
        { "Not", (x, _) => x.NotInPlace() },
    };

    private static void AssertInPlaceMatchesOperator(IEnumerable<int> a, IEnumerable<int> b,
        Func<RoaringBitmap, RoaringBitmap, RoaringBitmap> operation, Action<RoaringBitmap, RoaringBitmap> inPlace)
    {
        var x = RoaringBitmap.Create(a);
        var y = RoaringBitmap.Create(b);
        var expected = operation(x, y);

        inPlace(x, y);

        // Equal compares keys and containers structurally, so the in-place path must also pick the same
        // container representation as the operator, which serialization relies on
        Assert.Equal(expected, x);
        Assert.Equal(expected.Cardinality, x.Cardinality);
        Assert.Equal(RoaringBitmap.Create(b), y);
        using var ms = new MemoryStream();
        RoaringBitmap.Serialize(x, ms);
        ms.Position = 0;
        Assert.Equal(expected, RoaringBitmap.Deserialize(ms));
    }

    [Theory, MemberData(nameof(OperandPairs))]
    public void OrInPlace_MatchesOperator(IEnumerable<int> a, IEnumerable<int> b)
    {
        AssertInPlaceMatchesOperator(a, b, (x, y) => x | y, (x, y) => x.OrInPlace(y));
    }

    [Theory, MemberData(nameof(OperandPairs))]
    public void AndInPlace_MatchesOperator(IEnumerable<int> a, IEnumerable<int> b)
    {
        AssertInPlaceMatchesOperator(a, b, (x, y) => x & y, (x, y) => x.AndInPlace(y));
    }

    [Theory, MemberData(nameof(OperandPairs))]
    public void XorInPlace_MatchesOperator(IEnumerable<int> a, IEnumerable<int> b)
    {
        AssertInPlaceMatchesOperator(a, b, (x, y) => x ^ y, (x, y) => x.XorInPlace(y));
    }

    [Theory, MemberData(nameof(OperandPairs))]
    public void AndNotInPlace_MatchesOperator(IEnumerable<int> a, IEnumerable<int> b)
    {
        AssertInPlaceMatchesOperator(a, b, RoaringBitmap.AndNot, (x, y) => x.AndNotInPlace(y));
    }

    [Theory, MemberData(nameof(OperandPairs))]
    public void NotInPlace_MatchesOperator(IEnumerable<int> a, IEnumerable<int> b)
    {
        var x = RoaringBitmap.Create(a) | RoaringBitmap.Create(b);
        var expected = ~x;

        x.NotInPlace();
        Assert.Equal(expected, x);
        Assert.Equal(expected.Cardinality, x.Cardinality);

        x.NotInPlace();
        Assert.Equal(RoaringBitmap.Create(a) | RoaringBitmap.Create(b), x);
    }

    [Theory, MemberData(nameof(InPlaceOperations))]
    public void InPlace_OnBitmapsSharingContainers_LeavesTheOthersUntouched(string name, Action<RoaringBitmap, RoaringBitmap> inPlace)
    {
        var a = RoaringBitmap.Create(Range(0, 100)); // array container
        var b = RoaringBitmap.Create(Range(1 << 16, 10000)); // bitmap container
        var c = RoaringBitmap.Create(Range(50, 100).Concat(Range((1 << 16) + 5000, 10000)));
        var union = a | b; // shares the containers of a and b
        var clone = union.Clone();
        var optimized = union.Optimize();
        var expectedUnion = RoaringBitmap.Create(Range(0, 100).Concat(Range(1 << 16, 10000)));

        inPlace(union, c);
        Assert.Equal(RoaringBitmap.Create(Range(0, 100)), a);
        Assert.Equal(RoaringBitmap.Create(Range(1 << 16, 10000)), b);
        Assert.Equal(expectedUnion, clone);
        Assert.Equal(expectedUnion, optimized);

        inPlace(a, c);
        inPlace(clone, c);
        Assert.Equal(RoaringBitmap.Create(Range(1 << 16, 10000)), b);
        Assert.Equal(expectedUnion, optimized);
    }

    [Fact]
    public void InPlace_AccumulatorTakingOverContainers_LeavesTheSourceUntouched()
    {
        var sourceValues = Range(0, 100).Concat(Range(1 << 16, 10000)).ToList();
        var source = RoaringBitmap.Create(sourceValues);
        var expected = new HashSet<int>(sourceValues);

        var acc = RoaringBitmap.Create();
        acc.OrInPlace(source); // an empty accumulator takes over every container of the source
        acc.OrInPlace(RoaringBitmap.Create(Range(100, 100)));
        expected.UnionWith(Range(100, 100));
        acc.AndNotInPlace(RoaringBitmap.Create(Range(0, 10).Concat(Range(1 << 16, 10))));
        expected.ExceptWith(Range(0, 10).Concat(Range(1 << 16, 10)));
        acc.XorInPlace(RoaringBitmap.Create(Range(50, 100).Concat(Range((1 << 16) + 9000, 2000))));
        expected.SymmetricExceptWith(Range(50, 100).Concat(Range((1 << 16) + 9000, 2000)));
        acc.AndInPlace(RoaringBitmap.Create(Range(0, (1 << 16) + 10500)));
        expected.IntersectWith(Range(0, (1 << 16) + 10500));

        Assert.Equal(expected.Order(), acc.ToArray());
        Assert.Equal(RoaringBitmap.Create(sourceValues), source);
    }

    [Fact]
    public void Clone_CanBeModifiedIndependently()
    {
        var original = RoaringBitmap.Create(Range(0, 100).Concat(Range(1 << 16, 10000)));
        var clone = original.Clone();
        Assert.Equal(original, clone);
        Assert.NotSame(original, clone);

        clone.OrInPlace(RoaringBitmap.Create(100, (1 << 16) + 10000));
        original.AndNotInPlace(RoaringBitmap.Create(0, 1 << 16));

        Assert.Equal(RoaringBitmap.Create(Range(0, 101).Concat(Range(1 << 16, 10001))), clone);
        Assert.Equal(RoaringBitmap.Create(Range(1, 99).Concat(Range((1 << 16) + 1, 9999))), original);
    }

    [Fact]
    public void InPlace_NeverModifiesTheFullContainerSingletons()
    {
        var full = ~RoaringBitmap.Create(); // made of BitmapContainer.One
        var fullArray = RoaringBitmap.Create(Range(0, 4096)).Optimize(); // made of ArrayContainer.One

        full.AndNotInPlace(RoaringBitmap.Create(5));
        full.XorInPlace(RoaringBitmap.Create(1 << 16));
        full.NotInPlace();
        fullArray.OrInPlace(RoaringBitmap.Create(4096));
        fullArray.AndNotInPlace(RoaringBitmap.Create(0));

        Assert.Equal(new[] {5, 1 << 16}, full.ToArray());
        Assert.Equal(Range(1, 4096), fullArray.ToArray());
        Assert.Equal(Container.MaxCapacity, BitmapContainer.One.Cardinality);
        Assert.Equal(Container.MaxSize, ArrayContainer.One.Cardinality);
        var one = new List<int>();
        ArrayContainer.One.EnumerateFill(one, 0);
        Assert.Equal(Range(0, 4096), one);
    }

    [Fact]
    public void InPlace_WithItself()
    {
        var values = Range(0, 100).Concat(Range(1 << 16, 10000)).ToList();
        var x = RoaringBitmap.Create(values);

        x.OrInPlace(x);
        Assert.Equal(RoaringBitmap.Create(values), x);
        x.AndInPlace(x);
        Assert.Equal(RoaringBitmap.Create(values), x);
        x.XorInPlace(x);
        Assert.Equal(RoaringBitmap.Create(), x);

        var y = RoaringBitmap.Create(values);
        y.AndNotInPlace(y);
        Assert.Equal(RoaringBitmap.Create(), y);
    }

    [Fact]
    public void OrInPlace_OneValueAtATime_GrowsThroughTheContainerTypes()
    {
        var acc = RoaringBitmap.Create();
        for (var i = 0; i < 5000; i++)
        {
            acc.OrInPlace(RoaringBitmap.Create(2 * i, (1 << 16) + i));
        }
        Assert.Equal(RoaringBitmap.Create(Range(0, 5000).Select(i => 2 * i).Concat(Range(1 << 16, 5000))), acc);
    }
}
