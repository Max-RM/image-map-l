using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace TryashtarUtils.Utility;

// sort like file explorer does (e.g. 1->2 instead of 1->10)
public class LogicalStringComparer : IComparer<string>
{
    public static readonly LogicalStringComparer Instance = new();
    private LogicalStringComparer() { }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string? x, string? y);
    public int Compare(string? x, string? y)
    {
        return StrCmpLogicalW(x, y);
    }
}

public class LambdaComparer<TSource, TKey> : IComparer<TSource> where TKey : IComparable<TKey>
{
    private readonly Func<TSource?, TKey?> Selector;
    public LambdaComparer(Func<TSource?, TKey?> selector)
    {
        Selector = selector;
    }

    public int Compare(TSource? x, TSource? y)
    {
        var xKey = Selector(x);
        var yKey = Selector(y);
        if (xKey == null)
            return yKey == null ? 0 : -1;
        return xKey.CompareTo(yKey);
    }
}