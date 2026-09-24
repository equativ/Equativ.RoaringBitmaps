using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Equativ.RoaringBitmaps;

internal class RoaringArray : IEquatable<RoaringArray>
{
    private const int SerialCookieNoRuncontainer = 12346;
    private const int SerialCookie = 12347;
    private const int NoOffsetThreshold = 4;
    // _keys and _values may have spare capacity past _size once in-place operations have grown them
    private ushort[] _keys;
    private int _size;
    private Container[] _values;

    /// <summary>
    /// Use List directly, because the enumerator is a struct
    /// </summary>
    internal RoaringArray(int size, List<ushort> keys, List<Container> containers)
    {
        _size = size;
        _keys = new ushort[_size];
        _values = new Container[_size];
        for (var i = 0; i < _size; i++)
        {
            _keys[i] = keys[i];
            _values[i] = containers[i];
        }
        RecomputeCardinality();
    }

    private RoaringArray(int size, ushort[] keys, Container[] containers)
    {
        _size = size;
        _keys = keys;
        _values = containers;
        RecomputeCardinality();
    }

    public long Cardinality { get; private set; }

    private void RecomputeCardinality()
    {
        var cardinality = 0L;
        for (var i = 0; i < _size; i++)
        {
            cardinality += _values[i].Cardinality;
        }
        Cardinality = cardinality;
    }
    
    public void EnumerateFill(List<int> list)
    {
        for (var i = 0; i < _size; i++)
        {
            ushort key = _keys[i];
            int shiftedKey = key << 16;
            var container = _values[i];
            container.EnumerateFill(list, shiftedKey);
        }
    }

    public bool Equals(RoaringArray? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (ReferenceEquals(null, other))
        {
            return false;
        }
        if (_size != other._size)
        {
            return false;
        }
        for (var i = 0; i < _size; i++)
        {
            if (_keys[i] != other._keys[i] || !_values[i].Equals(other._values[i]))
            {
                return false;
            }
        }
        return true;
    }
    
    public override bool Equals(object? obj)
    {
        var ra = obj as RoaringArray;
        return ra != null && Equals(ra);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var code = 17;
            code = code * 23 + _size;
            for (var i = 0; i < _size; i++)
            {
                code = code * 23 + _keys[i].GetHashCode();
                code = code * 23 + _values[i].GetHashCode();
            }
            return code;
        }
    }

    private int AdvanceUntil(ushort key, int index)
    {
        return Utils.AdvanceUntil(_keys.AsSpan(0, _size), index, key);
    }

    /// <summary>
    /// A shallow copy: the containers are shared with this array, and marked as such so that neither side
    /// modifies them in place afterwards
    /// </summary>
    public RoaringArray Clone()
    {
        var keys = new ushort[_size];
        Array.Copy(_keys, keys, _size);
        var containers = new Container[_size];
        for (var i = 0; i < _size; i++)
        {
            containers[i] = _values[i].MarkShared();
        }
        return new RoaringArray(_size, keys, containers);
    }

    public static RoaringArray operator |(RoaringArray x, RoaringArray y)
    {
        var xLength = x._size;
        var yLength = y._size;
        var keys = new List<ushort>(xLength + yLength);
        var containers = new List<Container>(xLength + yLength);
        var size = 0;
        var xPos = 0;
        var yPos = 0;
        if (xPos < xLength && yPos < yLength)
        {
            var xKey = x._keys[xPos];
            var yKey = y._keys[yPos];
            while (true)
            {
                if (xKey == yKey)
                {
                    keys.Add(xKey);
                    containers.Add(x._values[xPos] | y._values[yPos]);
                    size++;
                    xPos++;
                    yPos++;
                    if (xPos == xLength || yPos == yLength)
                    {
                        break;
                    }
                    xKey = x._keys[xPos];
                    yKey = y._keys[yPos];
                }
                else if (xKey < yKey)
                {
                    keys.Add(xKey);
                    containers.Add(x._values[xPos].MarkShared());
                    size++;
                    xPos++;
                    if (xPos == xLength)
                    {
                        break;
                    }
                    xKey = x._keys[xPos];
                }
                else
                {
                    keys.Add(yKey);
                    containers.Add(y._values[yPos].MarkShared());
                    size++;
                    yPos++;
                    if (yPos == yLength)
                    {
                        break;
                    }
                    yKey = y._keys[yPos];
                }
            }
        }
        if (xPos == xLength)
        {
            for (var i = yPos; i < yLength; i++)
            {
                keys.Add(y._keys[i]);
                containers.Add(y._values[i].MarkShared());
                size++;
            }
        }
        else if (yPos == yLength)
        {
            for (var i = xPos; i < xLength; i++)
            {
                keys.Add(x._keys[i]);
                containers.Add(x._values[i].MarkShared());
                size++;
            }
        }
        return new RoaringArray(size, keys, containers);
    }

    public static RoaringArray operator &(RoaringArray x, RoaringArray y)
    {
        var xLength = x._size;
        var yLength = y._size;
        List<ushort>? keys = null;
        List<Container>? containers = null;
        var size = 0;
        var xPos = 0;
        var yPos = 0;
        while (xPos < xLength && yPos < yLength)
        {
            var xKey = x._keys[xPos];
            var yKey = y._keys[yPos];
            if (xKey == yKey)
            {
                var c = x._values[xPos] & y._values[yPos];
                if (c.Cardinality > 0)
                {
                    if (keys == null)
                    {
                        var length = Math.Min(xLength, yLength);
                        keys = new List<ushort>(length);
                        containers = new List<Container>(length);
                    }
                    keys.Add(xKey);
                    containers!.Add(c);
                    size++;
                }
                xPos++;
                yPos++;
            }
            else if (xKey < yKey)
            {
                xPos = x.AdvanceUntil(yKey, xPos);
            }
            else
            {
                yPos = y.AdvanceUntil(xKey, yPos);
            }
        }
        return new RoaringArray(size, keys!, containers!);
    }

    public static RoaringArray operator ^(RoaringArray x, RoaringArray y)
    {
        var xLength = x._size;
        var yLength = y._size;
        var keys = new List<ushort>(xLength + yLength);
        var containers = new List<Container>(xLength + yLength);
        var size = 0;
        var xPos = 0;
        var yPos = 0;
        if (xPos < xLength && yPos < yLength)
        {
            var xKey = x._keys[xPos];
            var yKey = y._keys[yPos];
            while (true)
            {
                if (xKey == yKey)
                {
                    var c = x._values[xPos] ^ y._values[yPos];
                    if (c.Cardinality > 0)
                    {
                        keys.Add(xKey);
                        containers.Add(c);
                        size++;
                    }

                    xPos++;
                    yPos++;
                    if (xPos == xLength || yPos == yLength)
                    {
                        break;
                    }
                    xKey = x._keys[xPos];
                    yKey = y._keys[yPos];
                }
                else if (xKey < yKey)
                {
                    keys.Add(xKey);
                    containers.Add(x._values[xPos].MarkShared());
                    size++;
                    xPos++;
                    if (xPos == xLength)
                    {
                        break;
                    }
                    xKey = x._keys[xPos];
                }
                else
                {
                    keys.Add(yKey);
                    containers.Add(y._values[yPos].MarkShared());
                    size++;
                    yPos++;
                    if (yPos == yLength)
                    {
                        break;
                    }
                    yKey = y._keys[yPos];
                }
            }
        }
        if (xPos == xLength)
        {
            for (var i = yPos; i < yLength; i++)
            {
                keys.Add(y._keys[i]);
                containers.Add(y._values[i].MarkShared());
                size++;
            }
        }
        else if (yPos == yLength)
        {
            for (var i = xPos; i < xLength; i++)
            {
                keys.Add(x._keys[i]);
                containers.Add(x._values[i].MarkShared());
                size++;
            }
        }
        return new RoaringArray(size, keys, containers);
    }

    public static RoaringArray operator ~(RoaringArray x)
    {
        var keys = new List<ushort>(Container.MaxCapacity);
        var size = 0;
        var containers = new List<Container>(Container.MaxCapacity);
        var oldIndex = 0;
        for (var i = 0; i < Container.MaxCapacity; i++)
        {
            var ushortI = (ushort) i;
            var index = Array.BinarySearch(x._keys, oldIndex, x._size - oldIndex, ushortI);
            if (index < 0)
            {
                keys.Add(ushortI);
                containers.Add(BitmapContainer.One);
                size++;
            }
            else
            {
                var c = x._values[index];
                if (!c.Equals(BitmapContainer.One)) // the bitwise negation of the one container is the zero container
                {
                    var nc = ~c;
                    if (nc.Cardinality > 0)
                    {
                        keys.Add(ushortI);
                        containers.Add(nc);
                        size++;
                    }
                }
                oldIndex = index;
            }
        }
        return new RoaringArray(size, keys, containers);
    }

    public static RoaringArray AndNot(RoaringArray x, RoaringArray y)
    {
        var xLength = x._size;
        var yLength = y._size;
        var keys = new List<ushort>(xLength);
        var containers = new List<Container>(xLength);
        var size = 0;
        var xPos = 0;
        var yPos = 0;
        while (xPos < xLength && yPos < yLength)
        {
            var xKey = x._keys[xPos];
            var yKey = y._keys[yPos];
            if (xKey == yKey)
            {
                var c = Container.AndNot(x._values[xPos], y._values[yPos]);
                if (c.Cardinality > 0)
                {
                    keys.Add(xKey);
                    containers.Add(c);
                    size++;
                }
                xPos++;
                yPos++;
            }
            else if (xKey < yKey)
            {
                var next = x.AdvanceUntil(yKey, xPos);
                for (var i = xPos; i < next; i++)
                {
                    keys.Add(x._keys[i]);
                    containers.Add(x._values[i].MarkShared());
                    size++;
                }
                xPos = next;
            }
            else
            {
                yPos = y.AdvanceUntil(xKey, yPos);
            }
        }
        if (yPos == yLength)
        {
            for (var i = xPos; i < xLength; i++)
            {
                keys.Add(x._keys[i]);
                containers.Add(x._values[i].MarkShared());
                size++;
            }
        }
        return new RoaringArray(size, keys, containers);
    }

    // In-place operations. They replace the content of this array with the result and never modify y.
    // Containers taken over from y are marked shared, and shared containers are never modified in place
    // (see Container.IsShared), so bitmaps sharing storage stay independent.

    public void OrInPlace(RoaringArray y)
    {
        MergeInPlace(y, false);
    }

    public void XorInPlace(RoaringArray y)
    {
        MergeInPlace(y, true);
    }

    private void MergeInPlace(RoaringArray y, bool xor)
    {
        if (ReferenceEquals(this, y))
        {
            if (xor)
            {
                Truncate(0);
            }
            return;
        }
        var xSize = _size;
        var ySize = y._size;
        var resultSize = UnionSize(_keys, xSize, y._keys, ySize);
        EnsureCapacity(resultSize);
        var keys = _keys;
        var values = _values;
        var yKeys = y._keys;
        var yValues = y._values;
        // Merge from the back, like Utils.MergeArraysInPlace: the write index k never overtakes the read index i
        // into this array's own entries, so nothing unread is overwritten. Every key of the union is written,
        // exactly filling [0, resultSize).
        var i = xSize - 1;
        var j = ySize - 1;
        var k = resultSize - 1;
        while (i >= 0 && j >= 0)
        {
            var xKey = keys[i];
            var yKey = yKeys[j];
            if (xKey > yKey)
            {
                keys[k] = xKey;
                values[k] = values[i];
                i--;
            }
            else if (xKey < yKey)
            {
                keys[k] = yKey;
                values[k] = yValues[j].MarkShared();
                j--;
            }
            else
            {
                keys[k] = xKey;
                values[k] = xor ? Container.XorInPlace(values[i], yValues[j]) : Container.OrInPlace(values[i], yValues[j]);
                i--;
                j--;
            }
            k--;
        }
        while (j >= 0)
        {
            keys[k] = yKeys[j];
            values[k] = yValues[j].MarkShared();
            j--;
            k--;
        }
        _size = resultSize;
        if (xor)
        {
            RemoveEmptyContainers(); // the xor of identical containers is empty
        }
        RecomputeCardinality();
    }

    private static int UnionSize(ushort[] xKeys, int xSize, ushort[] yKeys, int ySize)
    {
        var i = 0;
        var j = 0;
        var count = 0;
        while (i < xSize && j < ySize)
        {
            var xKey = xKeys[i];
            var yKey = yKeys[j];
            if (xKey <= yKey)
            {
                i++;
            }
            if (xKey >= yKey)
            {
                j++;
            }
            count++;
        }
        return count + (xSize - i) + (ySize - j);
    }

    public void AndInPlace(RoaringArray y)
    {
        RetainInPlace(y, false);
    }

    public void AndNotInPlace(RoaringArray y)
    {
        RetainInPlace(y, true);
    }

    /// <summary>
    /// this &amp; y, or AndNot(this, y) when andNot is set. The result never grows, so it is compacted forward
    /// into this array's own entries: the write index pos never overtakes the read index xPos.
    /// </summary>
    private void RetainInPlace(RoaringArray y, bool andNot)
    {
        if (ReferenceEquals(this, y))
        {
            if (andNot)
            {
                Truncate(0);
            }
            return;
        }
        var pos = 0;
        var xPos = 0;
        var yPos = 0;
        var xSize = _size;
        var ySize = y._size;
        while (xPos < xSize && yPos < ySize)
        {
            var xKey = _keys[xPos];
            var yKey = y._keys[yPos];
            if (xKey == yKey)
            {
                var c = andNot ? Container.AndNotInPlace(_values[xPos], y._values[yPos]) : Container.AndInPlace(_values[xPos], y._values[yPos]);
                if (c.Cardinality > 0)
                {
                    _keys[pos] = xKey;
                    _values[pos] = c;
                    pos++;
                }
                xPos++;
                yPos++;
            }
            else if (xKey < yKey)
            {
                var next = AdvanceUntil(yKey, xPos);
                if (andNot)
                {
                    pos = MoveEntries(xPos, next, pos); // keys missing from y are kept as they are
                }
                xPos = next;
            }
            else
            {
                yPos = y.AdvanceUntil(xKey, yPos);
            }
        }
        if (andNot)
        {
            pos = MoveEntries(xPos, xSize, pos);
        }
        Truncate(pos);
        RecomputeCardinality();
    }

    public void NotInPlace()
    {
        // every key of the 16 bit space is in the result, except those whose container is full
        var resultSize = Container.MaxCapacity;
        for (var i = 0; i < _size; i++)
        {
            if (_values[i].Cardinality == Container.MaxCapacity)
            {
                resultSize--;
            }
        }
        var keys = new ushort[resultSize];
        var values = new Container[resultSize];
        var pos = 0;
        var xPos = 0;
        for (var key = 0; key < Container.MaxCapacity; key++)
        {
            Container c;
            if (xPos < _size && _keys[xPos] == key)
            {
                c = Container.NotInPlace(_values[xPos++]);
                if (c.Cardinality == 0)
                {
                    continue;
                }
            }
            else
            {
                c = BitmapContainer.One;
            }
            keys[pos] = (ushort) key;
            values[pos++] = c;
        }
        _keys = keys;
        _values = values;
        _size = resultSize;
        RecomputeCardinality();
    }

    /// <summary>
    /// Moves the entries [from, to) down to position pos (pos &lt;= from) and returns the position following them
    /// </summary>
    private int MoveEntries(int from, int to, int pos)
    {
        var count = to - from;
        if (pos != from && count > 0)
        {
            Array.Copy(_keys, from, _keys, pos, count);
            Array.Copy(_values, from, _values, pos, count);
        }
        return pos + count;
    }

    private void RemoveEmptyContainers()
    {
        var pos = 0;
        for (var i = 0; i < _size; i++)
        {
            if (_values[i].Cardinality > 0)
            {
                _keys[pos] = _keys[i];
                _values[pos] = _values[i];
                pos++;
            }
        }
        Truncate(pos);
    }

    /// <summary>
    /// Shrinks to newSize entries, dropping the references to the containers past it
    /// </summary>
    private void Truncate(int newSize)
    {
        Array.Clear(_values, newSize, _size - newSize);
        _size = newSize;
    }

    private void EnsureCapacity(int required)
    {
        if (_keys.Length >= required)
        {
            return;
        }
        var capacity = Math.Max(required, Math.Min(2 * _keys.Length, Container.MaxCapacity));
        Array.Resize(ref _keys, capacity);
        Array.Resize(ref _values, capacity);
    }

    public static void Serialize(RoaringArray roaringArray, Stream stream)
    {
        var hasRun = HasRunContainer(roaringArray);
        using (var binaryWriter = new BinaryWriter(stream, Encoding.UTF8, true))
        {
            var size = roaringArray._size;
            var keys = roaringArray._keys;
            var values = roaringArray._values;
            var startOffset = 0;
            if (hasRun)
            {
                binaryWriter.Write(SerialCookie | ((size - 1) << 16));
                var bitmapOfRunContainers = new byte[(size + 7) / 8];
                for (var i = 0; i < size; ++i)
                {
                    if (values[i].Equals(ArrayContainer.One) || values[i].Equals(BitmapContainer.One))
                    {
                        bitmapOfRunContainers[i / 8] |= (byte) (1 << (i % 8));
                    }
                }
                binaryWriter.Write(bitmapOfRunContainers);
            }
            else // no run containers
            {
                binaryWriter.Write(SerialCookieNoRuncontainer);
                binaryWriter.Write(size);
                startOffset = 4 + 4 + 4 * size + 4 * size;
            }
            for (var k = 0; k < size; ++k)
            {
                binaryWriter.Write(keys[k]);
                binaryWriter.Write((ushort) (values[k].Cardinality - 1));
            }
            if (!hasRun || size >= NoOffsetThreshold)
            {
                for (var k = 0; k < size; k++)
                {
                    binaryWriter.Write(startOffset);
                    startOffset += values[k].ArraySizeInBytes;
                }
            }
            for (var k = 0; k < size; ++k)
            {
                var container = values[k];
                ArrayContainer ac;
                BitmapContainer bc;
                if ((ac = container as ArrayContainer) != null)
                {
                    if (ac.Equals(ArrayContainer.One))
                    {
                        binaryWriter.Write((ushort) 1);
                        binaryWriter.Write((ushort) 0);
                        binaryWriter.Write((ushort) (Container.MaxSize - 1));
                    }
                    else
                    {
                        ArrayContainer.Serialize(ac, binaryWriter);
                    }
                }
                else if ((bc = container as BitmapContainer) != null)
                {
                    if (bc.Equals(BitmapContainer.One))
                    {
                        binaryWriter.Write((ushort) 1);
                        binaryWriter.Write((ushort) 0);
                        binaryWriter.Write((ushort) (Container.MaxCapacity - 1));
                    }
                    else
                    {
                        BitmapContainer.Serialize(bc, binaryWriter);
                    }
                }
            }
            binaryWriter.Flush();
        }
    }

    private static bool HasRunContainer(RoaringArray roaringArray)
    {
        for (var i = 0; i < roaringArray._size; i++)
        {
            if (roaringArray._values[i].Equals(ArrayContainer.One) || roaringArray._values[i].Equals(BitmapContainer.One))
            {
                return true;
            }
        }
        return false;
    }

    public static RoaringArray Deserialize(Stream stream)
    {
        using (var binaryReader = new BinaryReader(stream, Encoding.UTF8, true))
        {
            var cookie = binaryReader.ReadUInt32();
            var lbcookie = cookie & 0xFFFF;
            if (lbcookie != SerialCookie && cookie != SerialCookieNoRuncontainer)
            {
                throw new InvalidDataException("No RoaringBitmap file.");
            }
            var hasRun = lbcookie == SerialCookie;
            var size = (int) (hasRun ? (cookie >> 16) + 1 : binaryReader.ReadUInt32());
            var keys = new ushort[size];
            var containers = new Container[size];
            var cardinalities = new int[size];
            var isBitmap = new bool[size];

            byte[] bitmapOfRunContainers = null;
            if (hasRun)
            {
                bitmapOfRunContainers = binaryReader.ReadBytes((size + 7) / 8);
            }
            for (var k = 0; k < size; ++k)
            {
                keys[k] = binaryReader.ReadUInt16();
                cardinalities[k] = 1 + (0xFFFF & binaryReader.ReadUInt16());
                isBitmap[k] = cardinalities[k] > Container.MaxSize;
                if (bitmapOfRunContainers != null && (bitmapOfRunContainers[k / 8] & (1 << (k % 8))) != 0)
                {
                    isBitmap[k] = false;
                }
            }
            if (!hasRun || size >= NoOffsetThreshold)
            {
                // skipping the offsets
                binaryReader.ReadBytes(size * 4);
            }
            for (var k = 0; k < size; ++k)
            {
                if (isBitmap[k])
                {
                    containers[k] = BitmapContainer.Deserialize(binaryReader, cardinalities[k]);
                }
                else if (bitmapOfRunContainers != null && (bitmapOfRunContainers[k / 8] & (1 << (k % 8))) != 0)
                {
                    var nbrruns = binaryReader.ReadUInt16();
                    var values = new List<ushort>(nbrruns * 2); // probably more
                    var count = 0;
                    var specialCase = false;
                    for (var j = 0; j < nbrruns; ++j)
                    {
                        var value = binaryReader.ReadUInt16();
                        var length = binaryReader.ReadUInt16();

                        if (nbrruns == 1 && value == 0 && length == Container.MaxCapacity - 1) // special one scenario
                        {
                            containers[k] = BitmapContainer.One;
                            specialCase = true;
                            break;
                        }
                        if (nbrruns == 1 && value == 0 && length == Container.MaxSize - 1) // special one scenario
                        {
                            containers[k] = ArrayContainer.One;
                            specialCase = true;
                            break;
                        }
                        for (int i = value; i < value + length + 1; i++)
                        {
                            values.Add((ushort) i);
                        }
                        count += length;
                    }
                    if (!specialCase)
                    {
                        if (count > Container.MaxSize)
                        {
                            containers[k] = BitmapContainer.Create(values.ToArray());
                        }
                        else
                        {
                            containers[k] = ArrayContainer.Create(values.ToArray());
                        }
                    }
                }
                else
                {
                    containers[k] = ArrayContainer.Deserialize(binaryReader, cardinalities[k]);
                }
            }
            for (var i = 0; i < size; i++)
            {
                if (containers[i].Equals(ArrayContainer.One))
                {
                    containers[i] = ArrayContainer.One;
                }
                else if (containers[i].Equals(BitmapContainer.One))
                {
                    containers[i] = BitmapContainer.One;
                }
            }
            return new RoaringArray(size, keys, containers);
        }
    }

    public static RoaringArray Optimize(RoaringArray roaringArray)
    {
        var result = roaringArray.Clone();
        for (var i = 0; i < result._size; i++)
        {
            var currentContainer = result._values[i];
            if (currentContainer.Equals(ArrayContainer.One))
            {
                result._values[i] = ArrayContainer.One;
            }
            else if (currentContainer.Equals(BitmapContainer.One))
            {
                result._values[i] = BitmapContainer.One;
            }
        }
        return result;
    }
}