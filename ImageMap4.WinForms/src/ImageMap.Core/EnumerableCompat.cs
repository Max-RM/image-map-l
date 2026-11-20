using System.Collections.Generic;

namespace ImageMap4
{
    internal static class EnumerableCompat
    {
        public static IEnumerable<T[]> Chunk<T>(this IEnumerable<T> source, int size)
        {
            var bucket = new List<T>(size);
            foreach (var item in source)
            {
                bucket.Add(item);
                if (bucket.Count == size)
                {
                    yield return bucket.ToArray();
                    bucket.Clear();
                }
            }
            if (bucket.Count > 0)
                yield return bucket.ToArray();
        }
    }
}
