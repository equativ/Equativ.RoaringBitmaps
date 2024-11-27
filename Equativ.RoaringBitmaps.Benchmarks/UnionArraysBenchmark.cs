using BenchmarkDotNet.Attributes;

namespace Equativ.RoaringBitmaps.Benchmark;

[MemoryDiagnoser(false)]
public class UnionArraysBenchmark
{
    private ushort[] set1;
    private ushort[] set2;
    private ushort[] buffer;

    [Params(100, 1000, 10000)]
    public int ArraySize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        set1 = new ushort[ArraySize];
        set2 = new ushort[ArraySize / 2];
        buffer = new ushort[ArraySize * 2];

        Random rng = new Random(42);
        for (int i = 0; i < set1.Length; i++)
        {
            set1[i] = (ushort)rng.Next(ushort.MaxValue);
        }
        for (int i = 0; i < set2.Length; i++)
        {
            set2[i] = (ushort)rng.Next(ushort.MaxValue);
        }

        Array.Sort(set1);
        Array.Sort(set2);
    }
    
    [Benchmark(Baseline = true)]
    public int Baseline()
    {
        return Utils.UnionArrays(set1, ArraySize, set2, ArraySize, buffer);
    }

    [Benchmark]
    public int Gpt()
    {
        return Utils.UnionArraysGpt(set1, ArraySize, set2, ArraySize, buffer);
    }
    
    [Benchmark]
    public int Lemire()
    {
        return Utils.UnionArraysLemire(set1, ArraySize, set2, ArraySize, buffer);
    }
}