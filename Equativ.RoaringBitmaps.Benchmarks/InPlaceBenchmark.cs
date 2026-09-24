using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Equativ.RoaringBitmaps.Datasets;

namespace Equativ.RoaringBitmaps.Benchmarks;

// Ran on Macbook pro M5 Pro, .NET 9, ShortRun job
// | Method            | FileName            | Mean        | Ratio | Allocated  | Alloc Ratio |
// |------------------ |-------------------- |------------:|------:|-----------:|------------:|
// | OrFold            | census1881.zip      | 2,910.31 us |  1.00 | 10996870 B |       1.000 |
// | OrFoldInPlace     | census1881.zip      | 2,369.50 us |  0.81 |  2015638 B |       0.183 |
// | OrFold            | weather_sept_85.zip | 3,134.13 us |  1.00 | 23584878 B |       1.000 |
// | OrFoldInPlace     | weather_sept_85.zip | 2,249.07 us |  0.72 |   161414 B |       0.007 |
// | XorFold           | census1881.zip      | 2,433.16 us |  1.00 | 10418590 B |       1.000 |
// | XorFoldInPlace    | census1881.zip      | 2,173.05 us |  0.89 |  1439382 B |       0.138 |
// | XorFold           | weather_sept_85.zip | 3,491.45 us |  1.00 | 23569670 B |       1.000 |
// | XorFoldInPlace    | weather_sept_85.zip | 2,651.22 us |  0.76 |   146742 B |       0.006 |
// | AndNotFold        | census1881.zip      |    29.09 us |  1.00 |    67664 B |       1.000 |
// | AndNotFoldInPlace | census1881.zip      |    19.04 us |  0.66 |      424 B |       0.006 |
// | AndNotFold        | weather_sept_85.zip |   240.28 us |  1.00 |  2364080 B |       1.000 |
// | AndNotFoldInPlace | weather_sept_85.zip |   134.39 us |  0.56 |   130640 B |       0.055 |
/// <summary>
/// Folds a whole dataset into a single accumulator, the typical use case of the in-place operations:
/// the operators allocate a new bitmap at every step, the in-place variants reuse the accumulator.
/// (And is left out: the intersection of a whole dataset is empty after a couple of steps.)
/// </summary>
[MemoryDiagnoser(false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class InPlaceBenchmark
{
    private RoaringBitmap[] _bitmaps;

    [Params(Paths.Census1881, Paths.WeatherSept85)]
    public string FileName { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        using var provider = new ZipRealDataProvider(FileName);
        _bitmaps = provider.ToArray();
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Or")]
    public long OrFold()
    {
        var acc = _bitmaps[0];
        for (var k = 1; k < _bitmaps.Length; k++)
        {
            acc = acc | _bitmaps[k];
        }
        return acc.Cardinality;
    }

    [Benchmark]
    [BenchmarkCategory("Or")]
    public long OrFoldInPlace()
    {
        var acc = _bitmaps[0].Clone();
        for (var k = 1; k < _bitmaps.Length; k++)
        {
            acc.OrInPlace(_bitmaps[k]);
        }
        return acc.Cardinality;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Xor")]
    public long XorFold()
    {
        var acc = _bitmaps[0];
        for (var k = 1; k < _bitmaps.Length; k++)
        {
            acc = acc ^ _bitmaps[k];
        }
        return acc.Cardinality;
    }

    [Benchmark]
    [BenchmarkCategory("Xor")]
    public long XorFoldInPlace()
    {
        var acc = _bitmaps[0].Clone();
        for (var k = 1; k < _bitmaps.Length; k++)
        {
            acc.XorInPlace(_bitmaps[k]);
        }
        return acc.Cardinality;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("AndNot")]
    public long AndNotFold()
    {
        var acc = _bitmaps[0];
        for (var k = 1; k < _bitmaps.Length; k++)
        {
            acc = RoaringBitmap.AndNot(acc, _bitmaps[k]);
        }
        return acc.Cardinality;
    }

    [Benchmark]
    [BenchmarkCategory("AndNot")]
    public long AndNotFoldInPlace()
    {
        var acc = _bitmaps[0].Clone();
        for (var k = 1; k < _bitmaps.Length; k++)
        {
            acc.AndNotInPlace(_bitmaps[k]);
        }
        return acc.Cardinality;
    }
}