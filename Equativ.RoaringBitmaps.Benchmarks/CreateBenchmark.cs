using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using Equativ.RoaringBitmaps.Datasets;

namespace Equativ.RoaringBitmaps.Benchmarks;

[MemoryDiagnoser(false)]
public class CreateBenchmark
{
    private List<int>[] _values;
    private readonly Consumer _consumer = new();
    
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
        using var provider = new ZipRealDataProvider(FileName);
        _values = provider.EnumerateValues().ToArray();
    }

    [Benchmark]
    public void Create()
    {
        for (var k = 0; k < _values.Length - 1; k++)
        {
            _consumer.Consume(RoaringBitmap.Create(_values[k]));
        }
    }
}