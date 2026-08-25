using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Sellora.CoreService.Tests;

public class JwtValidationTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public JwtValidationTests(TestWebAppFactory factory)
    {
        _factory = factory;
        _factory.CreateClient();
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

    private string CreateTokenSignedWithUntrustedKey()   // not static — needs _factory
    {
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

    [Fact]
    public async Task Request_WithExpiredToken_Returns401()
    {
        var client = _factory.CreateClient();

        // Token signed with the trusted test key but expired 5 minutes ago.
        var token = TestTokenFactory.CreateToken(
            _factory.Issuer, _factory.Audience,
            role: "SalesRep",
            lifetime: TimeSpan.FromMinutes(-5));

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/demo/sales-rep");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}