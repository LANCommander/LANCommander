using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using LANCommander.SDK.Enums;

namespace LANCommander.SDK.Extensions
{
    public static class ListExtensions
    {
        public static void RemoveRange<T>(this IList<T> collection, IEnumerable<T> itemsToRemove)
        {
            itemsToRemove = itemsToRemove?.ToArray() ?? [];
            foreach (var itemToRemove in itemsToRemove)
            {
                collection.Remove(itemToRemove);
            }
        }

        public static void RemoveAll<T>(this IList<T> collection, Predicate<T> match)
        {
            ArgumentNullException.ThrowIfNull(match);

            for (int i = collection.Count - 1; i >= 0; i--)
            {
                if (match(collection[i]))
                {
                    collection.RemoveAt(i);
                }
            }
        }
    }
}
