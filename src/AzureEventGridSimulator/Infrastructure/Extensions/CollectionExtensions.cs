namespace AzureEventGridSimulator.Infrastructure.Extensions;

public static class CollectionExtensions
{
    extension<T>(ICollection<T> collection)
    {
        public bool HasItems()
        {
            return collection != null && collection.Count != 0;
        }

        public string Separate(string separator = ", ", Func<T, string> toStringFunction = null)
        {
            toStringFunction ??= t => t.ToString();

            return string.Join(
                separator,
                (collection ?? Array.Empty<T>()).Select(c => toStringFunction(c))
            );
        }
    }
}
