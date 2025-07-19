using BenchmarkDotNet.Attributes;

namespace Equativ.RoaringBitmaps.Benchmarks;

public class PopcntBenchmark
{
    [ParamsSource(nameof(GetInputs))]
    public ulong[] Input { get; set; }

    public static IEnumerable<ulong[]> GetInputs()
    {
        yield return Enumerable.Range(0, 4).Select(x => (ulong)Random.Shared.NextInt64()).ToArray();
        yield return Enumerable.Range(0, 32).Select(x => (ulong)Random.Shared.NextInt64()).ToArray();
        yield return Enumerable.Range(0, 128).Select(x => (ulong)Random.Shared.NextInt64()).ToArray();
        yield return Enumerable.Range(0, 1024).Select(x => (ulong)Random.Shared.NextInt64()).ToArray();
    }
    
    [Benchmark]
    public int PopCount64()
    {
        return Popcnt64.Popcnt(Input);
    }

    [Benchmark]
    public int PopCountSimd()
    {
        return Utils.Popcnt(Input);
    }
}