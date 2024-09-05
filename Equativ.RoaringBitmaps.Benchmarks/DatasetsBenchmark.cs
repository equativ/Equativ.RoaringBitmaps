using BenchmarkDotNet.Attributes;
using Equativ.RoaringBitmaps.Datasets;

namespace Equativ.RoaringBitmaps.Benchmark;

[MemoryDiagnoser(false)]
public class DatasetsBenchmark
{
    private RoaringBitmap[] _mBitmaps;
    
    [Params(
        Paths.Census1881,
        Paths.Census1881Srt,
        Paths.CensusIncome,
        Paths.Census1881Srt,
        Paths.Dimension003,
        Paths.Dimension008,
        Paths.Dimension033,
        Paths.UsCensus2000,
        Paths.WeatherSept85,
        Paths.WeatherSept85Srt,
        Paths.WikileaksNoQuotes,
        Paths.WikileaksNoQuotesSrt)]
    public string FileName { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        using (var provider = new ZipRealDataProvider(FileName))
        {
            _mBitmaps = provider.ToArray();
        }
    }

    [Benchmark]
    public long Or()
    {
        var total = 0L;
        for (var k = 0; k < _mBitmaps.Length - 1; k++)
        {
            total += (_mBitmaps[k] | _mBitmaps[k + 1]).Cardinality;
        }
        return total;
    }

    [Benchmark]
    public long Xor()
    {
        var total = 0L;
        for (var k = 0; k < _mBitmaps.Length - 1; k++)
        {
            total += (_mBitmaps[k] ^ _mBitmaps[k + 1]).Cardinality;
        }
        return total;
    }

    [Benchmark]
    public long And()
    {
        var total = 0L;
        for (var k = 0; k < _mBitmaps.Length - 1; k++)
        {
            total += (_mBitmaps[k] & _mBitmaps[k + 1]).Cardinality;
        }
        return total;
    }

    [Benchmark]
    public long AndNot()
    {
        var total = 0L;
        for (var k = 0; k < _mBitmaps.Length - 1; k++)
        {
            total += RoaringBitmap.AndNot(_mBitmaps[k], _mBitmaps[k + 1]).Cardinality;
        }
        return total;
    }

    [Benchmark]
    public long Iterate()
    {
        var total = 0L;
        foreach (var roaringBitmap in _mBitmaps)
        {
            foreach (var @int in roaringBitmap)
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