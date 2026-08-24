using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Sellora.CoreService.Tests;

public class JwtValidationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public JwtValidationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Request_WithNoToken_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/whoami");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Request_WithTamperedSignature_Returns401()
    {
        var client = _factory.CreateClient();

        // Mint a token signed with a key the service does NOT trust.
        // The service only trusts keys from Identity Server's JWKS, so a token
        // signed with this throwaway key must be rejected on signature grounds.
        var token = CreateTokenSignedWithUntrustedKey();

        var request = new HttpRequestMessage(HttpMethod.Get, "/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string CreateTokenSignedWithUntrustedKey()
    {
        // A fresh RSA key generated here — NOT the Identity Server signing key.
        using var rsa = RSA.Create(2048);
        var securityKey = new RsaSecurityKey(rsa);
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.RsaSha256);

        var handler = new JwtSecurityTokenHandler();
        var token = new JwtSecurityToken(
            issuer: "https://13.61.228.129:9443/oauth2/token",
            audience: "ThNL_9YM7zUgl5k6XWXforF1NNga",
            claims: new[] { new System.Security.Claims.Claim("sub", "test-user") },
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return handler.WriteToken(token);
    }
}