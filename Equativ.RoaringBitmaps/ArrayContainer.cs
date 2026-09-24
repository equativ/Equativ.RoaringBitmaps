using System;
using System.Collections.Generic;
using System.IO;

namespace Equativ.RoaringBitmaps;

internal class ArrayContainer : Container, IEquatable<ArrayContainer>
{
    public static readonly ArrayContainer One;
    // _content may have spare capacity past _cardinality once in-place operations have grown it
    private ushort[] _content;
    private int _cardinality;

    static ArrayContainer()
    {
        var data = new ushort[MaxSize];
        for (ushort i = 0; i < MaxSize; i++)
        {
            data[i] = i;
        }
        One = new ArrayContainer(MaxSize, data);
        One.MarkShared();
    }

    private ArrayContainer(int cardinality, ushort[] data)
    {
        _content = data;
        _cardinality = cardinality;
    }

    protected internal override int Cardinality => _cardinality;

    public override int ArraySizeInBytes => _cardinality * sizeof(ushort);

    public bool Equals(ArrayContainer? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (ReferenceEquals(null, other))
        {
            return false;
        }
        if (_cardinality != other._cardinality)
        {
            return false;
        }
        for (var i = 0; i < _cardinality; i++)
        {
            if (_content[i] != other._content[i])
            {
                return false;
            }
        }
        return true;
    }

    internal static ArrayContainer Create(ushort[] values)
    {
        return new ArrayContainer(values.Length, values);
    }

    internal static ArrayContainer Create(BitmapContainer bc)
    {
        var data =  GC.AllocateUninitializedArray<ushort>(bc.Cardinality);
        var cardinality = bc.FillArray(data);
        var result = new ArrayContainer(cardinality, data);
        return result;
    }

    protected override bool EqualsInternal(Container other)
    {
        var ac = other as ArrayContainer;
        return ac != null && Equals(ac);
    }

    public override void EnumerateFill(List<int> list, int key)
    {
        for (var i = 0; i < _cardinality; i++)
        {
            list.Add(key | _content[i]);
        }
    }

    public static Container operator &(ArrayContainer x, ArrayContainer y)
    {
        var desiredCapacity = Math.Min(x._cardinality, y._cardinality);
        var data = new ushort[desiredCapacity];
        var calculatedCardinality = Utils.IntersectArrays(x._content.AsSpan(0, x._cardinality), y._content.AsSpan(0, y._cardinality), data);
        return new ArrayContainer(calculatedCardinality, data);
    }

    public static ArrayContainer operator &(ArrayContainer x, BitmapContainer y)
    {
        var data = new ushort[x._cardinality];
        return new ArrayContainer(Filter(x._content, x._cardinality, y, true, data), data);
    }

    public static Container operator |(ArrayContainer x, ArrayContainer y)
    {
        var totalCardinality = x._cardinality + y._cardinality;
        if (totalCardinality > MaxSize)
        {
            var output = new ushort[totalCardinality];
            var calcCardinality = Utils.UnionArrays(x._content, x._cardinality, y._content, y._cardinality, output);
            if (calcCardinality > MaxSize)
            {
                return BitmapContainer.Create(calcCardinality, output);
            }
            return new ArrayContainer(calcCardinality, output);
        }
        var desiredCapacity = totalCardinality;
        var data = new ushort[desiredCapacity];
        var calculatedCardinality = Utils.UnionArrays(x._content, x._cardinality, y._content, y._cardinality, data);
        return new ArrayContainer(calculatedCardinality, data);
    }

    public static Container operator |(ArrayContainer x, BitmapContainer y)
    {
        return y | x;
    }

    public static Container operator ~(ArrayContainer x)
    {
        return BitmapContainer.Create(x._cardinality, x._content, true); // an arraycontainer only contains up to 4096 values, so the negation is a bitmap container
    }

    public static Container operator ^(ArrayContainer x, ArrayContainer y)
    {
        var totalCardinality = x._cardinality + y._cardinality;
        if (totalCardinality > MaxSize)
        {
            var bc = BitmapContainer.CreateXor(x._content, x._cardinality, y._content, y._cardinality);
            return bc.Cardinality <= MaxSize ? Create(bc) : bc;
        }
        var data = new ushort[totalCardinality];
        var calculatedCardinality = Utils.XorArrays(x._content, x._cardinality, y._content, y._cardinality, data);
        return new ArrayContainer(calculatedCardinality, data);
    }

    public static Container operator ^(ArrayContainer x, BitmapContainer y)
    {
        return y ^ x;
    }

    public static Container AndNot(ArrayContainer x, ArrayContainer y)
    {
        var desiredCapacity = x._cardinality;
        var data = new ushort[desiredCapacity];
        var calculatedCardinality = Utils.DifferenceArrays(x._content, x._cardinality, y._content, y._cardinality, data);
        return new ArrayContainer(calculatedCardinality, data);
    }

    public static Container AndNot(ArrayContainer x, BitmapContainer y)
    {
        var data = new ushort[x._cardinality];
        return new ArrayContainer(Filter(x._content, x._cardinality, y, false, data), data);
    }

    /// <summary>
    /// Copies the values of source that are (contained = true) or are not (contained = false) in the bitmap
    /// to destination, which may be source itself. Returns the number of values copied.
    /// </summary>
    private static int Filter(ushort[] source, int count, BitmapContainer bitmap, bool contained, ushort[] destination)
    {
        var pos = 0;
        for (var i = 0; i < count; i++)
        {
            var v = source[i];
            if (bitmap.Contains(v) == contained)
            {
                destination[pos++] = v;
            }
        }
        return pos;
    }

    internal Container OrInPlace(ArrayContainer y)
    {
        var totalCardinality = _cardinality + y._cardinality;
        if (totalCardinality > MaxSize)
        {
            return this | y; // the result may no longer fit an array container, let the allocating path decide
        }
        EnsureCapacity(totalCardinality);
        _cardinality = Utils.MergeArraysInPlace(_content, _cardinality, y._content, y._cardinality, false);
        return this;
    }

    internal Container XorInPlace(ArrayContainer y)
    {
        var totalCardinality = _cardinality + y._cardinality;
        if (totalCardinality > MaxSize)
        {
            return this ^ y;
        }
        EnsureCapacity(totalCardinality);
        _cardinality = Utils.MergeArraysInPlace(_content, _cardinality, y._content, y._cardinality, true);
        return this;
    }

    // Intersection and difference never grow, so they are computed forward into the array itself:
    // the write position never overtakes the read position.

    internal ArrayContainer AndInPlace(ArrayContainer y)
    {
        _cardinality = Utils.IntersectArrays(_content.AsSpan(0, _cardinality), y._content.AsSpan(0, y._cardinality), _content);
        return this;
    }

    internal ArrayContainer AndInPlace(BitmapContainer y)
    {
        _cardinality = Filter(_content, _cardinality, y, true, _content);
        return this;
    }

    internal ArrayContainer AndNotInPlace(ArrayContainer y)
    {
        _cardinality = Utils.DifferenceArrays(_content, _cardinality, y._content, y._cardinality, _content);
        return this;
    }

    internal ArrayContainer AndNotInPlace(BitmapContainer y)
    {
        _cardinality = Filter(_content, _cardinality, y, false, _content);
        return this;
    }

    private void EnsureCapacity(int required)
    {
        if (_content.Length >= required)
        {
            return;
        }
        var newContent = GC.AllocateUninitializedArray<ushort>(Math.Max(required, Math.Min(2 * _content.Length, MaxSize)));
        Array.Copy(_content, newContent, _cardinality);
        _content = newContent;
    }

    public int OrArray(ulong[] bitmap)
    {
        var extraCardinality = 0;
        var yC = _cardinality;
        for (var i = 0; i < yC; i++)
        {
            var yValue = _content[i];
            var index = yValue >> 6;
            var previous = bitmap[index];
            var after = previous | (1UL << yValue);
            bitmap[index] = after;
            extraCardinality += (int) ((previous - after) >> 63);
        }
        return extraCardinality;
    }

    public int XorArray(ulong[] bitmap)
    {
        var extraCardinality = 0;
        var yC = _cardinality;
        for (var i = 0; i < yC; i++)
        {
            var yValue = _content[i];
            var index = yValue >> 6;
            var previous = bitmap[index];
            var mask = 1UL << yValue;
            bitmap[index] = previous ^ mask;
            extraCardinality += (int) (1 - 2 * ((previous & mask) >> yValue));
        }
        return extraCardinality;
    }


    public int AndNotArray(ulong[] bitmap)
    {
        var extraCardinality = 0;
        var yC = _cardinality;
        for (var i = 0; i < yC; i++)
        {
            var yValue = _content[i];
            var index = yValue >> 6;
            var previous = bitmap[index];
            var after = previous & ~(1UL << yValue);
            bitmap[index] = after;
            extraCardinality -= (int) ((previous ^ after) >> yValue);
        }
        return extraCardinality;
    }

    public override bool Equals(object? obj)
    {
        var ac = obj as ArrayContainer;
        return ac != null && Equals(ac);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var code = 17;
            code = code * 23 + _cardinality;
            for (var i = 0; i < _cardinality; i++)
            {
                code = code * 23 + _content[i];
            }
            return code;
        }
    }

    public static void Serialize(ArrayContainer ac, BinaryWriter binaryWriter)
    {
        for (var i = 0; i < ac._cardinality; i++)
        {
            binaryWriter.Write(ac._content[i]);
        }
    }

    public static ArrayContainer Deserialize(BinaryReader binaryReader, int cardinality)
    {
        var data = new ushort[cardinality];
        for (var i = 0; i < cardinality; i++)
        {
            data[i] = binaryReader.ReadUInt16();
        }
        return new ArrayContainer(cardinality, data);
    }
}