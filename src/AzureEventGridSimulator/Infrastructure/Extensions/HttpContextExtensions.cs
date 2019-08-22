using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AzureEventGridSimulator.Infrastructure.Extensions
{
    public static class HttpContextExtensions
    {
        public static async Task<string> RequestBody(this HttpContext context)
        {
            var buffer = new byte[Convert.ToInt32(context.Request.ContentLength)];
            await context.Request.Body.ReadAsync(buffer, 0, buffer.Length);
            context.Request.Body.Seek(0, SeekOrigin.Begin);
            var requestBody = Encoding.UTF8.GetString(buffer);
            return requestBody;
        }
    }
}
