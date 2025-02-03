using System.Text.RegularExpressions;
using LANCommander.SDK.Enums;

namespace LANCommander.Server.Services.Extensions;

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
}