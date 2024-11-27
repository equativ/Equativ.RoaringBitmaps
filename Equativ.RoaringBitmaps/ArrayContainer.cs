using System;
using System.Collections.Generic;
using System.IO;

namespace Equativ.RoaringBitmaps;

internal class ArrayContainer : Container, IEquatable<ArrayContainer>
{
    public static readonly ArrayContainer One;
    private readonly ushort[] _content;
    private readonly int _cardinality;

    static ArrayContainer()
    {
        var data = new ushort[MaxSize];
        for (ushort i = 0; i < MaxSize; i++)
        {
            data[i] = i;
        }
        One = new ArrayContainer(MaxSize, data);
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
        var data = new ushort[bc.Cardinality];
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
        var data = new ushort[x._content.Length];
        var c = x._cardinality;
        var pos = 0;
        for (var i = 0; i < c; i++)
        {
            var v = x._content[i];
            if (y.Contains(v))
            {
                data[pos++] = v;
            }
        }
        return new ArrayContainer(pos, data);
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
            var bc = BitmapContainer.CreateXor(x._content, x.Cardinality, y._content, y.Cardinality);
            if (bc.Cardinality <= MaxSize)
            {
                Create(bc);
            }
        }
        var desiredCapacity = totalCardinality;
        var data = new ushort[desiredCapacity];
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
        var data = new ushort[x._content.Length];
        var c = x._cardinality;
        var pos = 0;
        for (var i = 0; i < c; i++)
        {
            var v = x._content[i];
            if (!y.Contains(v))
            {
                data[pos++] = v;
            }
        }
        return new ArrayContainer(pos, data);
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