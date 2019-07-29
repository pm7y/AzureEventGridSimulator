using System;
using System.Collections.Generic;
using System.Linq;

namespace AzureEventGridSimulator.Extensions
{
    public static class CollectionExtensions
    {
        public static bool HasItems<T>(this ICollection<T> collection)
        {
            return collection != null && collection.Any();
        }

        public static string Separate<T>(this ICollection<T> collection, string separator = ", ", Func<T, string> toStringFunction = null)
        {
            if (toStringFunction == null)
            {
                toStringFunction = t => t.ToString();
            }

            return string.Join(separator, (collection ?? new T[0]).Select(c => toStringFunction(c)));
        }
    }
}
