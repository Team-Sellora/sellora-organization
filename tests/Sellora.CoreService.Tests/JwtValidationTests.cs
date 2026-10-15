using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Sellora.CoreService.Tests;

/// <summary>
/// JWT bearer validation end to end: a request without a usable token is
/// rejected with 401 before any authorization policy runs.
/// </summary>
/// <remarks>
/// <para>
/// Characteristic and blocks: the bearer token. Missing / signed with an
/// untrusted key / expired / valid. The valid block is covered by
/// RolePolicyTests (a SalesRep token gets 200), so it is not repeated here.
/// </para>
/// <para>
/// Boundary values: none numeric. The expired token is 5 minutes past its
/// lifetime, well outside the 30 second clock skew.
/// </para>
/// <para>
/// Coverage criterion: Each Choice over the invalid blocks, 3 rows. Each row
/// keeps the path its original Fact used, so the conversion does not change
/// what is exercised.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public class JwtValidationTests : IClassFixture<TestWebAppFactory>
{
    public enum TokenKind
    {
        Missing,
        UntrustedSignature,
        Expired
    }

    private readonly TestWebAppFactory _factory;

    public JwtValidationTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _factory.CreateClient();
    }

    [Theory]
    [InlineData(TokenKind.Missing, "/whoami")] // No Authorization header
    [InlineData(TokenKind.UntrustedSignature, "/whoami")] // Signed with a key the service does not trust
    [InlineData(TokenKind.Expired, "/api/demo/sales-rep")] // Trusted key, expired 5 minutes ago
    public async Task Request_WithMissingOrInvalidToken_Returns401(TokenKind tokenKind, string path)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = CreateRequest(path, CreateToken(tokenKind));

        // Act
        var response = await client.SendAsync(request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private string? CreateToken(TokenKind tokenKind)
    {
        return tokenKind switch
        {
            TokenKind.Missing => null,
            TokenKind.UntrustedSignature => CreateTokenSignedWithUntrustedKey(),
            // Token signed with the trusted test key but expired 5 minutes ago.
            TokenKind.Expired => TestTokenFactory.CreateToken(
                _factory.Issuer, _factory.Audience,
                role: "SalesRep",
                lifetime: TimeSpan.FromMinutes(-5)),
            _ => throw new ArgumentOutOfRangeException(nameof(tokenKind), tokenKind, null)
        };
    }

    private static HttpRequestMessage CreateRequest(string path, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);

        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return request;
    }

    private string CreateTokenSignedWithUntrustedKey()   // not static — needs _factory
    {
        // Mint a token signed with a key the service does NOT trust.
        // The service only trusts keys from Identity Server's JWKS, so a token
        // signed with this throwaway key must be rejected on signature grounds.
        using var rsa = RSA.Create(2048);
        var securityKey = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);
        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            issuer: _factory.Issuer,
            audience: _factory.Audience,
            claims: new[] { new System.Security.Claims.Claim("sub", "test-user") },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return handler.WriteToken(token);
    }
}