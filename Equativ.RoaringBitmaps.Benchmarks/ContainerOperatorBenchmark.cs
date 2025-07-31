using System;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Equativ.RoaringBitmaps.Benchmark;

[MemoryDiagnoser(false)]
public class ContainerOperatorBenchmark
{
    private Container[] _lhs = Array.Empty<Container>();
    private Container[] _rhs = Array.Empty<Container>();

    [Params(1000)]
    public int Size { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var rnd = new Random(42);
        _lhs = new Container[Size];
        _rhs = new Container[Size];
        for (int i = 0; i < Size; i++)
        {
            var start1 = rnd.Next(0, ushort.MaxValue - 200);
            var start2 = rnd.Next(0, ushort.MaxValue - 200);
            _lhs[i] = ArrayContainer.Create(Enumerable.Range(start1, 100).Select(x => (ushort)x).ToArray());
            _rhs[i] = ArrayContainer.Create(Enumerable.Range(start2, 100).Select(x => (ushort)x).ToArray());
        }
    }

    [Benchmark]
    public int Or()
    {
        int total = 0;
        for (int i = 0; i < _lhs.Length; i++)
        {
            total += (_lhs[i] | _rhs[i]).Cardinality;
        }
        return total;
    }

    [Benchmark]
    public int Xor()
    {
        int total = 0;
        for (int i = 0; i < _lhs.Length; i++)
        {
            total += (_lhs[i] ^ _rhs[i]).Cardinality;
        }
        return total;
    }

    [Benchmark]
    public int And()
    {
        int total = 0;
        for (int i = 0; i < _lhs.Length; i++)
        {
            total += (_lhs[i] & _rhs[i]).Cardinality;
        }
        return total;
    }

    [Benchmark]
    public int AndNot()
    {
        int total = 0;
        for (int i = 0; i < _lhs.Length; i++)
        {
            total += Container.AndNot(_lhs[i], _rhs[i]).Cardinality;
        }
        return total;
    }
}
