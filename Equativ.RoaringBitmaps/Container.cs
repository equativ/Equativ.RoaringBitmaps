using System;
using System.Collections.Generic;

namespace Equativ.RoaringBitmaps;

internal abstract class Container : IEquatable<Container>
{
    public const int MaxSize = 4096; // everything <= is an ArrayContainer
    public const int MaxCapacity = 1 << 16;

    protected internal abstract int Cardinality { get; }

    public abstract int ArraySizeInBytes { get; }

    /// <summary>
    /// True once this container may be referenced by more than one bitmap (or is a static singleton).
    /// A shared container is never mutated: in-place operations on it return a fresh container instead.
    /// The flag is sticky, it is never cleared.
    /// </summary>
    internal bool IsShared { get; private set; }

    internal Container MarkShared()
    {
        IsShared = true;
        return this;
    }

    public bool Equals(Container other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }
        if (ReferenceEquals(null, other))
        {
            return false;
        }
        return EqualsInternal(other);
    }

    protected abstract bool EqualsInternal(Container other);

    public abstract void EnumerateFill(List<int> list, int key);

    public static Container operator |(Container x, Container y)
    {
        var xArrayContainer = x as ArrayContainer;
        var yArrayContainer = y as ArrayContainer;
        if (xArrayContainer != null && yArrayContainer != null)
        {
            return xArrayContainer | yArrayContainer;
        }
        if (xArrayContainer != null)
        {
            return xArrayContainer | (BitmapContainer)y;
        }
        if (yArrayContainer != null)
        {
            return (BitmapContainer) x | yArrayContainer;
        }
        return (BitmapContainer) x | (BitmapContainer)y;
    }

    public static Container operator &(Container x, Container y)
    {
        var xArrayContainer = x as ArrayContainer;
        var yArrayContainer = y as ArrayContainer;
        if (xArrayContainer != null && yArrayContainer != null)
        {
            return xArrayContainer & yArrayContainer;
        }
        if (xArrayContainer != null)
        {
            return xArrayContainer & (BitmapContainer) y;
        }
        if (yArrayContainer != null)
        {
            return (BitmapContainer) x & yArrayContainer;
        }
        return (BitmapContainer) x & (BitmapContainer) y;
    }

    public static Container operator ^(Container x, Container y)
    {
        var xArrayContainer = x as ArrayContainer;
        var yArrayContainer = y as ArrayContainer;
        if (xArrayContainer != null && yArrayContainer != null)
        {
            return xArrayContainer ^ yArrayContainer;
        }
        if (xArrayContainer != null)
        {
            return xArrayContainer ^ (BitmapContainer)y;
        }
        if (yArrayContainer != null)
        {
            return (BitmapContainer) x ^ yArrayContainer;
        }
        return (BitmapContainer) x ^ (BitmapContainer)y;
    }

    public static Container operator ~(Container x)
    {
        var xArrayContainer = x as ArrayContainer;
        return xArrayContainer != null ? ~xArrayContainer : ~(BitmapContainer) x;
    }

    public static Container AndNot(Container x, Container y)
    {
        var xArrayContainer = x as ArrayContainer;
        var yArrayContainer = y as ArrayContainer;
        if (xArrayContainer != null && yArrayContainer != null)
        {
            return ArrayContainer.AndNot(xArrayContainer, yArrayContainer);
        }
        if (xArrayContainer != null)
        {
            return ArrayContainer.AndNot(xArrayContainer, (BitmapContainer)y);
        }
        if (yArrayContainer != null)
        {
            return BitmapContainer.AndNot((BitmapContainer) x, yArrayContainer);
        }
        return BitmapContainer.AndNot((BitmapContainer) x, (BitmapContainer)y);
    }

    // The in-place variants below compute the same results as the operators above but reuse the storage of x
    // whenever x is not shared and the result fits its representation. They return the container holding the
    // result: x itself when it was updated in place, a new container otherwise. y is never modified.

    public static Container OrInPlace(Container x, Container y)
    {
        if (x.IsShared)
        {
            return x | y;
        }
        if (x is ArrayContainer xa)
        {
            return y is ArrayContainer ya ? xa.OrInPlace(ya) : xa | (BitmapContainer) y;
        }
        var xb = (BitmapContainer) x;
        return y is ArrayContainer yac ? xb.OrInPlace(yac) : xb.OrInPlace((BitmapContainer) y);
    }

    public static Container AndInPlace(Container x, Container y)
    {
        if (x.IsShared)
        {
            return x & y;
        }
        if (x is ArrayContainer xa)
        {
            return y is ArrayContainer ya ? xa.AndInPlace(ya) : xa.AndInPlace((BitmapContainer) y);
        }
        var xb = (BitmapContainer) x;
        return y is ArrayContainer yac ? xb & yac : xb.AndInPlace((BitmapContainer) y);
    }

    public static Container XorInPlace(Container x, Container y)
    {
        if (x.IsShared)
        {
            return x ^ y;
        }
        if (x is ArrayContainer xa)
        {
            return y is ArrayContainer ya ? xa.XorInPlace(ya) : xa ^ (BitmapContainer) y;
        }
        var xb = (BitmapContainer) x;
        return y is ArrayContainer yac ? xb.XorInPlace(yac) : xb.XorInPlace((BitmapContainer) y);
    }

    public static Container AndNotInPlace(Container x, Container y)
    {
        if (x.IsShared)
        {
            return AndNot(x, y);
        }
        if (x is ArrayContainer xa)
        {
            return y is ArrayContainer ya ? xa.AndNotInPlace(ya) : xa.AndNotInPlace((BitmapContainer) y);
        }
        var xb = (BitmapContainer) x;
        return y is ArrayContainer yac ? xb.AndNotInPlace(yac) : xb.AndNotInPlace((BitmapContainer) y);
    }

    public static Container NotInPlace(Container x)
    {
        // the negation of an array container is always a bitmap container, so there is nothing to reuse
        return x.IsShared || x is ArrayContainer ? ~x : ((BitmapContainer) x).NotInPlace();
    }
}