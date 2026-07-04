using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace StructureOfArraysGenerator;

public static class ListExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RemoveAtSwapBack<T>(this IList<T> list, int index)
    {
        list[index] = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
    }
}