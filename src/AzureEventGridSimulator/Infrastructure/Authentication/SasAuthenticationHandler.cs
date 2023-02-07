namespace AzureEventGridSimulator.Infrastructure.Authentication;

using System;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using System.Web;
using AzureEventGridSimulator.Domain;
using AzureEventGridSimulator.Infrastructure.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

public class SasAuthenticationHandler : AuthenticationHandler<SasAuthenticationOptions>
{
    public SasAuthenticationHandler(IOptionsMonitor<SasAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock)
        : base(options, logger, encoder, clock)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        static Task<AuthenticateResult> Fail()
        {
            return Task.FromResult(AuthenticateResult.Fail("\"The request did not contain a valid aeg-sas-key or aeg-sas-token.\""));
        }

        Task<AuthenticateResult> Success()
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "Authenticated user")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        var topicKey = Context.Features.Get<TopicSettings>()?.Key ?? throw new InvalidOperationException("Topic is not available");
        if (Request.Headers.ContainsKey(Constants.AegSasKeyHeader))
        {
            if (!string.Equals(Request.Headers[Constants.AegSasKeyHeader].First(), topicKey, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError("'aeg-sas-key' value did not match the expected value!");
                return Fail();
            }

            return Success();
        }

        if (Request.Headers.ContainsKey(Constants.AegSasTokenHeader))
        {
            if (!TokenIsValid(Request.Headers[Constants.AegSasTokenHeader].First(), topicKey))
            {
                Logger.LogError("'aeg-sas-key' value did not match the expected value!");
                return Fail();
            }

            return Success();
        }

        if (Request.Headers.ContainsKey(HeaderNames.Authorization))
        {
            var token = Request.Headers[HeaderNames.Authorization].ToString();
            if (token.StartsWith(Constants.SasAuthorizationType) && !TokenIsValid(token.Replace(Constants.SasAuthorizationType, "").Trim(), topicKey))
            {
                Logger.LogError("'Authorization: SharedAccessSignature' value did not match the expected value!");
                return Fail();
            }

            return Success();
        }

        return Fail();
    }

    private bool TokenIsValid(string token, string key)
    {
        var query = HttpUtility.ParseQueryString(token);
        var decodedResource = HttpUtility.UrlDecode(query["r"], Encoding.UTF8);
        var decodedExpiration = HttpUtility.UrlDecode(query["e"], Encoding.UTF8);
        var encodedSignature = query["s"];

        if (!DateTime.TryParse(decodedExpiration, out var tokenExpiryDateTime)
            || tokenExpiryDateTime.ToUniversalTime() <= DateTime.UtcNow)
        {
            return false;
        }

        var encodedResource = HttpUtility.UrlEncode(decodedResource);
        var encodedExpiration = HttpUtility.UrlEncode(decodedExpiration);

        var unsignedSas = $"r={encodedResource}&e={encodedExpiration}";

        using var hmac = new HMACSHA256(Convert.FromBase64String(key));
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(unsignedSas)));
        var encodedComputedSignature = HttpUtility.UrlEncode(signature);

        if (encodedSignature == signature)
        {
            return true;
        }

        Logger.LogWarning("{ExpectedSignature} != {MessageSignature}", encodedComputedSignature, signature);

        return false;
    }
}
