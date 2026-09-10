using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace Sellora.CoreService.Tests;

/// <summary>
/// The RSA signing key used by tests. The test host is configured to trust its
/// public half, so <see cref="TestTokenFactory"/> can mint validly-signed tokens
/// that exercise authorization (403) rather than only authentication (401).
/// </summary>
public static class TestSigningKey
{
    private static readonly RSA Rsa = RSA.Create(2048);

    /// <summary>The security key the test host trusts as the issuer signing key.</summary>
    public static readonly RsaSecurityKey SecurityKey = new(Rsa) { KeyId = "test-signing-key" };
}