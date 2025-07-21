using System.Collections;
using System.IO.Compression;

namespace Equativ.RoaringBitmaps.Datasets;

public class ZipRealDataProvider : IEnumerable<RoaringBitmap>, IDisposable
{
    private readonly ZipArchive _mArchive;

    public ZipRealDataProvider(string path)
    {
        var fs = File.OpenRead(path);
        _mArchive = new ZipArchive(fs, ZipArchiveMode.Read);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public IEnumerator<RoaringBitmap> GetEnumerator()
    {
        foreach (var zipArchiveEntry in _mArchive.Entries)
        {
            using (var stream = zipArchiveEntry.Open())
            {
                using (var stringReader = new StreamReader(stream))
                {
                    var split = stringReader.ReadLine().Split(',');
                    var values = split.Select(int.Parse).ToList();
                    var bitmap = RoaringBitmap.Create(values);
                    yield return bitmap.Optimize();
                }
            }
        }
    }
    
    public IEnumerable<List<int>> EnumerateValues()
    {
        foreach (var zipArchiveEntry in _mArchive.Entries)
        {
            using (var stream = zipArchiveEntry.Open())
            {
                using (var stringReader = new StreamReader(stream))
                {
                    var split = stringReader.ReadLine().Split(',');
                    var values = split.Select(int.Parse).ToList();
                    yield return values;
                }
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    ~ZipRealDataProvider()
    {
        Dispose(false);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _mArchive.Dispose();
        }
    }
}