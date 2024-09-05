using Equativ.RoaringBitmaps.Datasets;
using Xunit;
using Xunit.Abstractions;

namespace Equativ.RoaringBitmaps.Tests;

public class BenchmarkTests : IClassFixture<BenchmarkTests.BenchmarkTestsFixture>
{
    private readonly BenchmarkTestsFixture _mFixture;
    private readonly ITestOutputHelper _mOutputHelper;

    public BenchmarkTests(BenchmarkTestsFixture fixture, ITestOutputHelper outputHelper)
    {
        _mFixture = fixture;
        _mOutputHelper = outputHelper;
    }

    [Theory]
    [InlineData(Paths.CensusIncome, 12487395)]
    [InlineData(Paths.Census1881, 2007691)]
    [InlineData(Paths.Dimension003, 7733676)]
    [InlineData(Paths.Dimension008, 5555233)]
    [InlineData(Paths.Dimension033, 7579526)]
    [InlineData(Paths.UsCensus2000, 11954)]
    [InlineData(Paths.WeatherSept85, 24729002)]
    [InlineData(Paths.WikileaksNoQuotes, 541893)]
    [InlineData(Paths.CensusIncomeSrt, 11257282)]
    [InlineData(Paths.Census1881Srt, 1360167)]
    [InlineData(Paths.WeatherSept85Srt, 30863347)]
    [InlineData(Paths.WikileaksNoQuotesSrt, 574463)]
    public void Or(string name, int value)
    {
        var bitmaps = _mFixture.GetBitmaps(name);
        Assert.NotNull(bitmaps);
        var total = 0L;
        for (var k = 0; k < bitmaps.Length - 1; k++)
        {
            total += (bitmaps[k] | bitmaps[k + 1]).Cardinality;
        }
        Assert.Equal(value, total);
    }

    [Theory]
    [InlineData(Paths.CensusIncome, 11241947)]
    [InlineData(Paths.Census1881, 2007668)]
    [InlineData(Paths.Dimension003, 7733676)]
    [InlineData(Paths.Dimension008, 5442916)]
    [InlineData(Paths.Dimension033, 7579526)]
    [InlineData(Paths.UsCensus2000, 11954)]
    [InlineData(Paths.WeatherSept85, 24086983)]
    [InlineData(Paths.WikileaksNoQuotes, 538566)]
    [InlineData(Paths.CensusIncomeSrt, 10329567)]
    [InlineData(Paths.Census1881Srt, 1359961)]
    [InlineData(Paths.WeatherSept85Srt, 29800358)]
    [InlineData(Paths.WikileaksNoQuotesSrt, 574311)]
    public void Xor(string name, int value)
    {
        var bitmaps = _mFixture.GetBitmaps(name);
        Assert.NotNull(bitmaps);
        var total = 0L;
        for (var k = 0; k < bitmaps.Length - 1; k++)
        {
            total += (bitmaps[k] ^ bitmaps[k + 1]).Cardinality;
        }
        Assert.Equal(value, total);
    }

    [Theory]
    [InlineData(Paths.CensusIncome, 1245448)]
    [InlineData(Paths.Census1881, 23)]
    [InlineData(Paths.Dimension003, 0)]
    [InlineData(Paths.Dimension008, 112317)]
    [InlineData(Paths.Dimension033, 0)]
    [InlineData(Paths.UsCensus2000, 0)]
    [InlineData(Paths.WeatherSept85, 642019)]
    [InlineData(Paths.WikileaksNoQuotes, 3327)]
    [InlineData(Paths.CensusIncomeSrt, 927715)]
    [InlineData(Paths.Census1881Srt, 206)]
    [InlineData(Paths.WeatherSept85Srt, 1062989)]
    [InlineData(Paths.WikileaksNoQuotesSrt, 152)]
    public void And(string name, int value)
    {
        var bitmaps = _mFixture.GetBitmaps(name);
        Assert.NotNull(bitmaps);
        var total = 0L;
        for (var k = 0; k < bitmaps.Length - 1; k++)
        {
            total += (bitmaps[k] & bitmaps[k + 1]).Cardinality;
        }
        Assert.Equal(value, total);
    }

    [Theory]
    [InlineData(Paths.CensusIncome, -942184551)]
    [InlineData(Paths.Census1881, 246451066)]
    [InlineData(Paths.Dimension003, -1287135055)]
    [InlineData(Paths.Dimension008, -423436314)]
    [InlineData(Paths.Dimension033, -1287135055)]
    [InlineData(Paths.UsCensus2000, -1260727955)]
    [InlineData(Paths.WeatherSept85, 644036874)]
    [InlineData(Paths.WikileaksNoQuotes, 413846869)]
    [InlineData(Paths.CensusIncomeSrt, -679313956)]
    [InlineData(Paths.Census1881Srt, 445584405)]
    [InlineData(Paths.WeatherSept85Srt, 1132748056)]
    [InlineData(Paths.WikileaksNoQuotesSrt, 1921022163)]
    public void Iterate(string name, int value)
    {
        var bitmaps = _mFixture.GetBitmaps(name);
        Assert.NotNull(bitmaps);
        var total = 0;
        foreach (var roaringBitmap in bitmaps)
        {
            foreach (var @int in roaringBitmap)
            {
                unchecked
                {
                    total += @int;
                }
            }
        }
        Assert.Equal(value, total);
    }

    // The Dimension data sets are simply too slow
    [Theory]
    [InlineData(Paths.CensusIncome)]
    [InlineData(Paths.Census1881)]
    //[InlineData(Paths.Dimension003)]
    //[InlineData(Paths.Dimension008)]
    //[InlineData(Paths.Dimension033)]
    [InlineData(Paths.UsCensus2000)]
    [InlineData(Paths.WeatherSept85)]
    [InlineData(Paths.WikileaksNoQuotes)]
    [InlineData(Paths.CensusIncomeSrt)]
    [InlineData(Paths.Census1881Srt)]
    [InlineData(Paths.WeatherSept85Srt)]
    [InlineData(Paths.WikileaksNoQuotesSrt)]
    public void Not(string name)
    {
        var bitmaps = _mFixture.GetBitmaps(name);
        Assert.NotNull(bitmaps);
        foreach (var roaringBitmap in bitmaps)
        {
            var doublenegated = ~~roaringBitmap;
            Assert.Equal(roaringBitmap, doublenegated);
        }
    }

    [Theory]
    [InlineData(Paths.CensusIncome, 5666586)]
    [InlineData(Paths.Census1881, 1003836)]
    [InlineData(Paths.Dimension003, 3866831)]
    [InlineData(Paths.Dimension008, 2721459)]
    [InlineData(Paths.Dimension033, 3866842)]
    [InlineData(Paths.UsCensus2000, 5970)]
    [InlineData(Paths.WeatherSept85, 11960876)]
    [InlineData(Paths.WikileaksNoQuotes, 271605)]
    [InlineData(Paths.CensusIncomeSrt, 5164671)]
    [InlineData(Paths.Census1881Srt, 679375)]
    [InlineData(Paths.WeatherSept85Srt, 14935706)]
    [InlineData(Paths.WikileaksNoQuotesSrt, 286904)]
    public void AndNot(string name, int value)
    {
        var bitmaps = _mFixture.GetBitmaps(name);
        Assert.NotNull(bitmaps);
        var total = 0L;
        for (var k = 0; k < bitmaps.Length - 1; k++)
        {
            total += RoaringBitmap.AndNot(bitmaps[k], bitmaps[k + 1]).Cardinality;
        }
        Assert.Equal(value, total);
    }

    [Theory]
    [InlineData(Paths.CensusIncome)]
    [InlineData(Paths.Census1881)]
    [InlineData(Paths.Dimension003)]
    [InlineData(Paths.Dimension008)]
    [InlineData(Paths.Dimension033)]
    [InlineData(Paths.UsCensus2000)]
    [InlineData(Paths.WeatherSept85)]
    [InlineData(Paths.WikileaksNoQuotes)]
    [InlineData(Paths.CensusIncomeSrt)]
    [InlineData(Paths.Census1881Srt)]
    [InlineData(Paths.WeatherSept85Srt)]
    [InlineData(Paths.WikileaksNoQuotesSrt)]
    public void SerializeDeserialize(string name)
    {
        var bitmaps = _mFixture.GetBitmaps(name);
        Assert.NotNull(bitmaps);
        foreach (var roaringBitmap in bitmaps)
        {
            using (var ms = new MemoryStream())
            {
                RoaringBitmap.Serialize(roaringBitmap, ms);
                ms.Position = 0;
                var rb2 = RoaringBitmap.Deserialize(ms);
                Assert.Equal(roaringBitmap, rb2);
            }
        }
    }

    [Theory]
    [InlineData(new[] { 0b_0000_0000UL }, 0)]
    [InlineData(new[] { 0b_0000_0001UL }, 1)]
    [InlineData(new[] { 0b_1000_0000UL }, 1)]
    [InlineData(new[] { 0b_1111_1111UL }, 8)]
    [InlineData(new[] { 0b_1000_0000_0000_0000UL }, 1)]
    [InlineData(new[] { 0b_1000_0000_0000_0000_1000_0000_0000_0000_1000_0000_0000_0000_1000_0000_0000_0000UL }, 4)]
    [InlineData(new[] { 0b_1010_0000_0010_0011_0000_0010_0000_0100_1000_0000_0000_1100_1010_0001_1000_0010UL }, 15)]
    [InlineData(new[] { 0b_0000_0001UL, 0b_0000_0001UL }, 2)]
    [InlineData(new[] { 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL }, 3)]
    [InlineData(new[] { 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL }, 4)]
    [InlineData(new[] { 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL }, 7)]
    [InlineData(new[] { 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL, 0b_0000_0001UL }, 8)]
    [InlineData(new[] { 0b_0000_0001UL, 0b_0000_0011UL, 0b_0000_0111UL, 0b_0000_1111UL, 0b_0001_1111UL, 0b_0011_1111UL, 0b_0111_1111UL, 0b_1111_11111UL }, 37)]
    [InlineData(new ulong[] { 1453523523442, 9218234235626264, 1293481512390459239, 2384583424912, 28923795749884, 9234848923478387, 9919238781987590878 }, 184)]
    [InlineData(new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32 }, 81)]
    public void PopCount(ulong[] data, int expectedCount)
    {
        Assert.Equal(expectedCount, Utils.Popcnt(data));
    }

    public class BenchmarkTestsFixture
    {
        private readonly Dictionary<string, RoaringBitmap[]> _mBitmapDictionary = new Dictionary<string, RoaringBitmap[]>();

        public RoaringBitmap[] GetBitmaps(string name)
        {
            RoaringBitmap[] bitmaps;
            if (!_mBitmapDictionary.TryGetValue(name, out bitmaps))
            {
                using (var provider = new ZipRealDataProvider(name))
                {
                    bitmaps = provider.ToArray();
                    _mBitmapDictionary[name] = bitmaps;
                }
            }
            return bitmaps;
        }
    }
}