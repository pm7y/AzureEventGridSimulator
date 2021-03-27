namespace AzureEventGridSimulator.Infrastructure.Extensions
{
    public static class StringExtensions
    {
        public static string Otherwise(this string input, string otherwise)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return otherwise;
            }

            return input;
        }
    }
}
