using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Infrastructure.Extensions
{
    public static class HttpContextExtensions
    {
        public static async Task<string> RequestBody(this HttpContext context)
        {
            var reader = new StreamReader(context.Request.Body);
            reader.BaseStream.Seek(0, SeekOrigin.Begin);
            return await reader.ReadToEndAsync();
        }
    }
}
