using BenchmarkDotNet.Attributes;
using Equativ.RoaringBitmaps.Datasets;
using Roaring.Net.CRoaring;

namespace Equativ.RoaringBitmaps.Benchmark;

// Ran on Macbook pro M1
// ⚠️Unmanaged allocation happening in CRoaring are not tracked by the memory diagnoser.
// | Method           | FileName            | Mean         | Error      | StdDev       | Median       | Allocated   |
// |----------------- |-------------------- |-------------:|-----------:|-------------:|-------------:|------------:|
// | Or               | census1881.zip      |    166.13 us |   1.406 us |     1.247 us |    166.06 us |   379.61 KB |
// | Or_CRoaring      | census1881.zip      |    351.65 us |   1.779 us |     1.486 us |    351.33 us |     6.22 KB |
// | Xor              | census1881.zip      |    166.42 us |   2.989 us |     2.650 us |    166.26 us |   379.61 KB |
// | Xor_CRoaring     | census1881.zip      |    408.27 us |   6.659 us |     9.115 us |    405.43 us |     6.22 KB |
// | And              | census1881.zip      |     29.45 us |   0.210 us |     0.164 us |     29.42 us |    34.52 KB |
// | And_CRoaring     | census1881.zip      |     37.83 us |   0.745 us |     0.696 us |     37.53 us |     6.22 KB |
// | Iterate          | census1881.zip      |  5,198.82 us |  25.519 us |    19.924 us |  5,195.58 us |  3996.69 KB |
// | Iterate_CRoaring | census1881.zip      |  1,601.64 us |  20.682 us |    19.346 us |  1,596.48 us |  3927.85 KB |
// | Or               | dimension_008.zip   |  1,016.78 us |   4.848 us |     4.535 us |  1,017.50 us |  5252.52 KB |
// | Or_CRoaring      | dimension_008.zip   |  1,434.72 us |   3.293 us |     2.750 us |  1,434.91 us |   163.72 KB |
// | Xor              | dimension_008.zip   |  1,080.76 us |   8.126 us |     6.785 us |  1,082.03 us |  5308.54 KB |
// | Xor_CRoaring     | dimension_008.zip   |  1,708.83 us |   7.622 us |     7.130 us |  1,707.99 us |   163.72 KB |
// | And              | dimension_008.zip   |    473.00 us |   5.988 us |     4.675 us |    471.56 us |  1587.58 KB |
// | And_CRoaring     | dimension_008.zip   |    800.10 us |   1.501 us |     1.404 us |    800.11 us |   163.72 KB |
// | Iterate          | dimension_008.zip   | 17,770.79 us |  71.707 us |    63.566 us | 17,785.19 us | 11811.16 KB |
// | Iterate_CRoaring | dimension_008.zip   |  5,524.00 us |  13.029 us |    11.550 us |  5,521.85 us | 11207.29 KB |
// | Or               | dimension_033.zip   |  1,489.69 us |  18.102 us |    16.047 us |  1,489.77 us |  3590.46 KB |
// | Or_CRoaring      | dimension_033.zip   |  1,359.99 us |   4.910 us |     4.593 us |  1,358.54 us |     5.38 KB |
// | Xor              | dimension_033.zip   |  1,511.59 us |   6.556 us |     5.475 us |  1,512.56 us |  3591.55 KB |
// | Xor_CRoaring     | dimension_033.zip   |  1,506.95 us |   6.901 us |     6.455 us |  1,506.57 us |     5.38 KB |
// | And              | dimension_033.zip   |    486.69 us |   2.857 us |     2.386 us |    486.00 us |  1056.69 KB |
// | And_CRoaring     | dimension_033.zip   |    370.42 us |   5.733 us |     4.476 us |    368.35 us |     5.38 KB |
// | Iterate          | dimension_033.zip   | 22,393.90 us | 144.802 us |   135.447 us | 22,391.34 us | 15212.93 KB |
// | Iterate_CRoaring | dimension_033.zip   |  6,449.73 us |  56.394 us |    47.092 us |  6,452.17 us | 15113.17 KB |
// | Or               | weather_sept_85.zip |  9,005.99 us |  43.411 us |    40.607 us |  9,001.96 us | 14372.18 KB |
// | Or_CRoaring      | weather_sept_85.zip |  5,718.97 us |  74.741 us |    62.412 us |  5,715.33 us |     6.23 KB |
// | Xor              | weather_sept_85.zip |  9,632.77 us | 147.642 us |   123.288 us |  9,615.90 us | 14516.96 KB |
// | Xor_CRoaring     | weather_sept_85.zip |  6,856.77 us |  17.323 us |    15.357 us |  6,855.70 us |     6.23 KB |
// | And              | weather_sept_85.zip |  6,772.61 us |  29.253 us |    25.932 us |  6,766.63 us |  3783.54 KB |
// | And_CRoaring     | weather_sept_85.zip |  5,267.61 us |  19.212 us |    17.971 us |  5,271.02 us |     6.23 KB |
// | Iterate          | weather_sept_85.zip | 74,620.41 us | 252.467 us |   236.158 us | 74,613.52 us | 50459.65 KB |
// | Iterate_CRoaring | weather_sept_85.zip | 27,164.46 us | 595.106 us | 1,697.871 us | 26,547.94 us | 50293.08 KB |
[MemoryDiagnoser(false)]
public class CRoaringBenchmark
{
    private RoaringBitmap[] _bitmaps;
    private Roaring32Bitmap[] _bitmapsCRoaring;
    
    [Params(
        // Paths.Census1881,
        // Paths.Dimension008,
        Paths.Dimension033)]
        //Paths.WeatherSept85)]
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
    public long Or()
    {
        long total = 0L;
        for (var k = 0; k < _bitmaps.Length - 1; k++)
        {
            total += (_bitmaps[k] | _bitmaps[k + 1]).Cardinality;
        }
        return total;
    }
    
    [Benchmark]
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
    public long Xor()
    {
        long total = 0L;
        for (var k = 0; k < _bitmaps.Length - 1; k++)
        {
            total += (_bitmaps[k] ^ _bitmaps[k + 1]).Cardinality;
        }
        return total;
    }

    [Benchmark]
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
    public long And()
    {
        long total = 0L;
        for (var k = 0; k < _bitmaps.Length - 1; k++)
        {
            total += (_bitmaps[k] & _bitmaps[k + 1]).Cardinality;
        }
        return total;
    }

    [Benchmark]
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

    [Benchmark]
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