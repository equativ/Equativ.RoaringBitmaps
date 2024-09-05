using System.Numerics;
using System.Runtime.CompilerServices;

namespace Equativ.RoaringBitmaps;

internal static class Popcnt64
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Popcnt(ulong[] xArray)
    {
        var result = 0;
        for (var i = 0; i < xArray.Length; i++)
        {
            result += BitOperations.PopCount(xArray[i]);
        }

        return result;
    }
}