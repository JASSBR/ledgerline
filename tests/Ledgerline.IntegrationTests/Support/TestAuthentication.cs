using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ledgerline.IntegrationTests.Support;

/// <summary>
/// Stands in for Keycloak: "Authorization: Test sub=…;roles=customer" yields the same claims a Keycloak token carries
/// ("sub", "preferred_username", flat "roles"), so authorization code runs unchanged.
/// </summary>
internal sealed class TestAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith(SchemeName + " ", StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var values = header[(SchemeName.Length + 1)..].Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => pair[1], StringComparer.Ordinal);
        var claims = new List<Claim> { new("sub", values["sub"]), new("preferred_username", values["sub"]) };
        claims.AddRange(values.GetValueOrDefault("roles", string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries).Select(role => new Claim("roles", role)));

        var identity = new ClaimsIdentity(claims, SchemeName, "preferred_username", "roles");
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

internal static class TestUsers
{
    public static AuthenticationHeaderValue Customer(string userId) => new(TestAuthenticationHandler.SchemeName, $"sub={userId};roles=customer");

    public static AuthenticationHeaderValue Operator => new(TestAuthenticationHandler.SchemeName, "sub=operator-1;roles=operator");
}
