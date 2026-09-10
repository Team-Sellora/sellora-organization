using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Mints JWTs signed with <see cref="TestSigningKey"/> for use in tests.
/// Issuer and audience are supplied by the caller (from test configuration),
/// so no service URLs are hardcoded here.
/// </summary>
public static class TestTokenFactory
{
  /// <summary>
  /// Creates a validly-signed test token carrying the given role and company.
  /// </summary>
  public static string CreateToken(
     string issuer,
     string audience,
     string role,
     string companyId = "COMP-001",
     string sub = "test-user",
     TimeSpan? lifetime = null)
  {
    var credentials = new SigningCredentials(
        TestSigningKey.SecurityKey,
        SecurityAlgorithms.RsaSha256);

    var claims = new List<Claim>
    {
        new("sub", sub),
        new("roles", role),
        new(ClaimTypes.Role, role),
        new("companyId", companyId),
    };

    var span = lifetime ?? TimeSpan.FromMinutes(5);

    var token = new JwtSecurityToken(
        issuer: issuer,
        audience: audience,
        claims: claims,
        notBefore: DateTime.UtcNow.AddMinutes(-10),  // valid-from in the past
        expires: DateTime.UtcNow.Add(span),
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
  }
}