using LANCommander.Launcher.Models.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LANCommander.Launcher.Services.Extensions
{
    public static class IEnumerableExtensions
    {
        static Regex TitleComparisonExpression = new Regex(@"^(?:a|the|an)\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static IOrderedEnumerable<T> OrderByTitle<T>(this IEnumerable<T> items, Func<T, string> keySelector, SortDirection direction = SortDirection.Ascending)
        {
            switch (direction)
            {
                case SortDirection.Ascending:
                default:
                    return items.OrderBy(i =>
                    {
                        var key = keySelector.Invoke(i);

                        return TitleComparisonExpression.Replace(key, "");
                    });

                case SortDirection.Descending:
                    return items.OrderByDescending(i =>
                    {
                        var key = keySelector.Invoke(i);

                        return TitleComparisonExpression.Replace(key, "");
                    });
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
    }
}
