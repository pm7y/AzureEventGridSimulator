using System.Collections.Generic;
using System.Linq;

namespace AzureEventGridSimulator.Extensions
{
    internal static class ICollectionExtensions
    {
        public static bool HasItems<T>(this ICollection<T> collection)
        {
            return collection != null && collection.Any();
        }
    }
}
