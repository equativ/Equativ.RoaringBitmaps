using BenchmarkDotNet.Running;
using Equativ.RoaringBitmaps.Datasets;

namespace Equativ.RoaringBitmaps.Benchmark;

internal class Program
{
    private static void Main(string[] args)
    {
        DatasetsBenchmark bm = new();
        bm.FileName = Paths.WeatherSept85;
        bm.Setup();
        while (true)
        {
            bm.Iterate();
        }

        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}