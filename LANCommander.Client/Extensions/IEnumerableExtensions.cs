using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LANCommander.Client.Extensions
{
    internal static class IEnumerableExtensions
    {
        static Regex TitleComparisonExpression = new Regex(@"^(?:a|the|an)\s*", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        internal static IOrderedEnumerable<T> OrderByTitle<T>(this IEnumerable<T> items, Func<T, string> keySelector)
        {
            return items.OrderBy(i =>
            {
                var key = keySelector.Invoke(i);

                return TitleComparisonExpression.Replace(key, "");
            });
        }
    }
}
