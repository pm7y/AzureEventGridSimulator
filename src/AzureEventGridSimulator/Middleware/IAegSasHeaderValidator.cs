using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Middleware
{
    public interface IAegSasHeaderValidator
    {
        bool IsValid(IHeaderDictionary requestHeaders, string topicKey);
    }
}
