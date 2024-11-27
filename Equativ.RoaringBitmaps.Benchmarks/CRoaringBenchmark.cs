using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Equativ.RoaringBitmaps.Datasets;
using Roaring.Net.CRoaring;

namespace Equativ.RoaringBitmaps.Benchmark;

// Ran on Macbook pro M1
// ⚠️Unmanaged allocation happening in CRoaring are not tracked by the memory diagnoser.
// | Method           | FileName       | Mean        | Error     | StdDev    | Allocated  |
// |----------------- |--------------- |------------:|----------:|----------:|-----------:|
// | Or               | census1881.zip |   182.35 us |  2.488 us |  2.205 us |  379.61 KB |
// | Or_CRoaring      | census1881.zip |   349.98 us |  0.854 us |  0.713 us |    6.22 KB |
// | Xor              | census1881.zip |   168.71 us |  0.403 us |  0.357 us |  379.61 KB |
// | Xor_CRoaring     | census1881.zip |   397.52 us |  2.415 us |  2.141 us |    6.22 KB |
// | And              | census1881.zip |    26.67 us |  0.108 us |  0.096 us |   34.52 KB |
// | And_CRoaring     | census1881.zip |    39.90 us |  0.142 us |  0.119 us |    6.22 KB |
// | Iterate          | census1881.zip | 2,222.27 us |  5.975 us |  4.989 us | 3934.13 KB |
// | Iterate_CRoaring | census1881.zip | 1,611.77 us | 14.444 us | 12.804 us | 3928.25 KB |
[MemoryDiagnoser(false)]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class CRoaringBenchmark
{
    private RoaringBitmap[] _bitmaps;
    private Roaring32Bitmap[] _bitmapsCRoaring;
    
    [Params(Paths.Census1881)]
    // [Params(Paths.Census1881, Paths.Dimension008, Paths.Dimension033, Paths.WeatherSept85)]
    public string FileName { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        using (var provider = new ZipRealDataProvider(FileName))
        {
            _bitmaps = provider.ToArray();
        }
        _bitmapsCRoaring = _bitmaps.Select(x => Roaring32Bitmap.FromValues(x.Select(y => (uint)y).ToArray())).ToArray();
    }

    [Benchmark]
    [BenchmarkCategory("Or")]
    public long Or()
    {
        long total = 0L;
        for (var k = 0; k < _bitmaps.Length - 1; k++)
        {
            total += (_bitmaps[k] | _bitmaps[k + 1]).Cardinality;
        }
        return total;
    }
    
    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Or")]
    public long Or_CRoaring()
    {
        long total = 0L;
        for (var k = 0; k < _bitmapsCRoaring.Length - 1; k++)
        {
            using var bitmap = _bitmapsCRoaring[k].Or(_bitmapsCRoaring[k + 1]);
            total += (long)bitmap.Count;
        }
        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Xor")]
    public long Xor()
    {
        long total = 0L;
        for (var k = 0; k < _bitmaps.Length - 1; k++)
        {
            total += (_bitmaps[k] ^ _bitmaps[k + 1]).Cardinality;
        }
        return total;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Xor")]
    public long Xor_CRoaring()
    {
        long total = 0L;
        for (var k = 0; k < _bitmapsCRoaring.Length - 1; k++)
        {
            using var bitmap = _bitmapsCRoaring[k].Xor(_bitmapsCRoaring[k + 1]);
            total += (long)bitmap.Count;
        }
        return total;
    }

    [Benchmark]
    [BenchmarkCategory("And")]
    public long And()
    {
        long total = 0L;
        for (var k = 0; k < _bitmaps.Length - 1; k++)
        {
            total += (_bitmaps[k] & _bitmaps[k + 1]).Cardinality;
        }
        return total;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("And")]
    public long And_CRoaring()
    {
        long total = 0L;
        for (var k = 0; k < _bitmapsCRoaring.Length - 1; k++)
        {
            using var bitmap = _bitmapsCRoaring[k].And(_bitmapsCRoaring[k + 1]);
            total += (long)bitmap.Count;
        }
        return total;
    }

    [Benchmark]
    [BenchmarkCategory("Iterate")]
    public long Iterate()
    {
        long total = 0L;
        foreach (var roaringBitmap in _bitmaps)
        {
            foreach (var @int in roaringBitmap.ToArray())
            {
                unchecked
                {
                    total += @int;
                }
            }
        }
        return total;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Iterate")]
    public long Iterate_CRoaring()
    {
        long total = 0L;
        foreach (var roaringBitmap in _bitmapsCRoaring)
        {
            foreach (var @int in roaringBitmap.ToArray())
            {
                unchecked
                {
                    total += @int;
                }
            }
        }
        return total;
    }
}