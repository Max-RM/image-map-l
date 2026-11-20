using System;
using System.Collections.Generic;
using System.Linq;

namespace TryashtarUtils.Utility;

public static class ListUtils
{
    // shuffle a list in-place
    public static void Shuffle<T>(IList<T> list, Random random)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = random.Next(n + 1);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }

    public static bool ExactlyOne<T>(this IEnumerable<T> items)
    {
        return items.Any() && !items.Skip(1).Any();
    }

    public static bool CountGreaterThan<T>(this IEnumerable<T> items, int count)
    {
        return items.Skip(count).Any();
    }

    // Enumerable.Range() but for longs
    public static IEnumerable<long> CreateRange(long start, long count)
    {
        var limit = start + count;
        while (start < limit)
        {
            yield return start;
            start++;
        }
    }

    public static Dictionary<TKey, TValue> Copy<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict) where TKey : notnull
    {
        return dict.ToDictionary(x => x.Key, y => y.Value);
    }

    public static int BinarySearch<T>(this IList<T> source, int index, int count, T item, IComparer<T>? comparer)
    {
        return Array.BinarySearch<T>(source.Cast<T>().ToArray(), index, count, item, comparer);
    }

    public static int BinarySearch<T>(this IList<T> source, T item)
    {
        return BinarySearch(source, 0, source.Count, item, null);
    }

    public static int BinarySearch<T>(this IList<T> source, T item, IComparer<T>? comparer)
    {
        return BinarySearch(source, 0, source.Count, item, comparer);
    }

    public static IEnumerable<T> Flatten<T>(T[,] arr)
    {
        for (int y = 0; y < arr.GetLength(1); y++)
        {
            for (int x = 0; x < arr.GetLength(0); x++)
            {
                yield return arr[x, y];
            }
        }
    }

    public static TTo[,] Map2D<TFrom, TTo>(TFrom[,] arr, Func<TFrom, TTo> func)
    {
        var result = new TTo[arr.GetLength(0), arr.GetLength(1)];
        for (int y = 0; y < arr.GetLength(1); y++)
        {
            for (int x = 0; x < arr.GetLength(0); x++)
            {
                result[x, y] = func(arr[x, y]);
            }
        }
        return result;
    }
}