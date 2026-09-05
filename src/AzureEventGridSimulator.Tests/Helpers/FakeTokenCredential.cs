using Azure.Core;

namespace AzureEventGridSimulator.Tests.Helpers;

/// <summary>
///     A credential that returns a static, non-validated token. The simulator's management API
///     accepts any bearer token, so this lets the real ARM client authenticate without Azure AD
///     (i.e. without requiring 'az login') in tests and local development.
/// </summary>
public class FakeTokenCredential : TokenCredential
{
    private static readonly AccessToken Token = new("fake-token", DateTimeOffset.MaxValue);

    public override AccessToken GetToken(
        TokenRequestContext requestContext,
        CancellationToken ct
    ) => Token;

    public override ValueTask<AccessToken> GetTokenAsync(
        TokenRequestContext requestContext,
        CancellationToken ct
    ) => new(Token);
}
