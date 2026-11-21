using System;
using System.Collections.Generic;
using System.Linq;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Extensions
{
    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Orders by title the way a library shelf would: leading articles ("The", "A", "An") are
        /// ignored and the comparison is case-insensitive. See <see cref="StringExtensions.ToSortableTitle(string?)"/>.
        /// </summary>
        public static IOrderedEnumerable<T> OrderByTitle<T>(this IEnumerable<T> items, Func<T, string?> keySelector, SortDirection direction = SortDirection.Ascending)
        {
            switch (direction)
            {
                case SortDirection.Descending:
                    return items.OrderByDescending(i => keySelector(i).ToSortableTitle(), StringComparer.CurrentCultureIgnoreCase);

                case SortDirection.Ascending:
                default:
                    return items.OrderBy(i => keySelector(i).ToSortableTitle(), StringComparer.CurrentCultureIgnoreCase);
            }
        }

        public static IOrderedEnumerable<TSource> OrderBy<TSource, TKey>(this IEnumerable<TSource> items, Func<TSource, TKey> keySelector, SortDirection direction)
        {
            switch (direction)
            {
                case SortDirection.Descending:
                    return items.OrderByDescending(keySelector);
                case SortDirection.Ascending:
                default:
                    return items.OrderBy(keySelector);
            }
        }
        
        public static bool HasAny<T>(this IEnumerable<T>? source)
            => source?.Any() == true;
        
        public static bool HasAny<T>(this IEnumerable<T>? source, Func<T, bool> predicate)
            => source?.Any(predicate) == true;
    }
}
