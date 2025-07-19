using BenchmarkDotNet.Running;
using Equativ.RoaringBitmaps.Datasets;

namespace Equativ.RoaringBitmaps.Benchmarks;

internal class Program
{
    private static void Main(string[] args)
    {
        // DatasetsBenchmark bm = new();
        // bm.FileName = Paths.Dimension008;
        // bm.Setup();
        // while (true)
        // {
        //     bm.And();
        // }
        
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
    }
}