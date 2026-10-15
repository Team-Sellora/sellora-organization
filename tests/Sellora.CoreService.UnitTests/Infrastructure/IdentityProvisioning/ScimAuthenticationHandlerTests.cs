using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Sellora.CoreService.Application.IdentityProvisioning;
using Sellora.CoreService.Infrastructure.IdentityProvisioning;

namespace Sellora.CoreService.UnitTests.Infrastructure.IdentityProvisioning;

/// <summary>
/// Credentials added to SCIM2 calls: basic admin credentials for local
/// development, otherwise a cached client-credentials token.
/// </summary>
/// <remarks>
/// <para>
/// IsConfigured Theory. Characteristics and blocks: C1 BaseUrl: set (base) /
/// empty. C2 AuthMode: ClientCredentials (base) / Basic. C3 credentials for
/// that mode: complete (base) / one missing.
/// </para>
/// <para>
/// Boundary values: none numeric.
/// </para>
/// <para>
/// Coverage criterion: Base Choice, 1 + (2-1) + (2-1) + (2-1) = 4 rows, plus
/// one extra row (Basic with a missing password). The Base Choice rows only
/// show the ClientCredentials credential check returning false; the extra
/// row shows the Basic credential check returning false as well.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ScimAuthenticationHandlerTests
{
  [Fact]
  public async Task BasicMode_SendsAdminCredentials()
  {
    var scim = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
    var client = ClientThrough(
      Handler(
        new IdentityProvisioningOptions
        {
          AuthMode = "basic",
          AdminUserName = "admin",
          AdminPassword = "secret"
        },
        new StubHttpHandler(_ => throw new InvalidOperationException("no token call expected")),
        scim));

    await client.GetAsync("scim2/Users");

    scim.Requests.Single().Authorization
      .Should().Be($"Basic {Base64("admin:secret")}");
  }

  [Fact]
  public async Task ClientCredentials_FetchesTokenOnce_AndReusesItUntilExpiry()
  {
    var tokenServer = new StubHttpHandler(_ =>
      StubHttpHandler.Json(HttpStatusCode.OK, "{\"access_token\":\"token-1\",\"expires_in\":3600}"));
    var scim = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
    var cache = new ScimTokenCache();
    var options = ClientCredentialsOptions();

    await ClientThrough(Handler(options, tokenServer, scim, cache)).GetAsync("scim2/Users");
    // A new handler (IHttpClientFactory rebuilds them) still uses the shared cache.
    await ClientThrough(Handler(options, tokenServer, scim, cache)).GetAsync("scim2/Users");

    tokenServer.Requests.Should().ContainSingle();
    var tokenRequest = tokenServer.Requests[0];
    tokenRequest.PathAndQuery.Should().Be("/oauth2/token");
    tokenRequest.Authorization.Should().Be($"Basic {Base64("client%20id:client-secret")}");
    tokenRequest.Body.Should().Contain("grant_type=client_credentials");

    scim.Requests.Should().HaveCount(2)
      .And.OnlyContain(request => request.Authorization == "Bearer token-1");
  }

  [Fact]
  public async Task ClientCredentials_WithoutExpiresIn_StillCachesTheToken()
  {
    var tokenServer = new StubHttpHandler(_ =>
      StubHttpHandler.Json(HttpStatusCode.OK, "{\"access_token\":\"token-2\"}"));
    var scim = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
    var cache = new ScimTokenCache();
    var options = ClientCredentialsOptions(tokenEndpoint: "https://login.test/custom/token");

    var client = ClientThrough(Handler(options, tokenServer, scim, cache));
    await client.GetAsync("scim2/Users");
    await ClientThrough(Handler(options, tokenServer, scim, cache)).GetAsync("scim2/Users");

    tokenServer.Requests.Should().ContainSingle()
      .Which.PathAndQuery.Should().Be("/custom/token");
    scim.Requests.Should().OnlyContain(request => request.Authorization == "Bearer token-2");
  }

  [Fact]
  public async Task ClientCredentials_RefusedByIdentityServer_ReportsProviderUnavailable()
  {
    var tokenServer = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
    var scim = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));

    var act = () => ClientThrough(Handler(ClientCredentialsOptions(), tokenServer, scim))
      .GetAsync("scim2/Users");

    (await act.Should().ThrowAsync<IdentityProvisioningUnavailableException>())
      .WithMessage("*refused the provisioning client's credentials (HTTP 401)*");
    scim.Requests.Should().BeEmpty();
  }

  [Theory]
  [InlineData("https://is.test/", "ClientCredentials", "client id", "client-secret", "", "", true)] // Base: base URL, client credentials complete
  [InlineData("", "ClientCredentials", "client id", "client-secret", "", "", false)] // C1 base URL empty
  [InlineData("https://is.test/", "Basic", "", "", "admin", "secret", true)] // C2 Basic mode, admin credentials complete
  [InlineData("https://is.test/", "ClientCredentials", "id", "", "", "", false)] // C3 client secret missing
  [InlineData("https://is.test/", "Basic", "", "", "admin", "", false)] // Extra: Basic mode, admin password missing
  public void IsConfigured_BaseUrlAndCredentialsForMode_ReturnsWhetherProvisioningCanRun(
    string baseUrl,
    string authMode,
    string clientId,
    string clientSecret,
    string adminUserName,
    string adminPassword,
    bool expected)
  {
    // Arrange
    var options = new IdentityProvisioningOptions
    {
      BaseUrl = baseUrl,
      AuthMode = authMode,
      ClientId = clientId,
      ClientSecret = clientSecret,
      AdminUserName = adminUserName,
      AdminPassword = adminPassword
    };

    // Act
    var result = options.IsConfigured;

    // Assert
    result.Should().Be(expected);
  }

  [Fact]
  public void ResolvedTokenEndpoint_NoTokenEndpointSet_AppendsOAuthPathToBaseUrl()
  {
    // Arrange
    var options = new IdentityProvisioningOptions { BaseUrl = "https://is.test/" };

    // Act
    var result = options.ResolvedTokenEndpoint;

    // Assert
    result.Should().Be("https://is.test/oauth2/token");
  }

  private static IdentityProvisioningOptions ClientCredentialsOptions(string tokenEndpoint = "") => new()
  {
    BaseUrl = "https://is.test/",
    ClientId = "client id",
    ClientSecret = "client-secret",
    TokenEndpoint = tokenEndpoint
  };

  private static ScimAuthenticationHandler Handler(
    IdentityProvisioningOptions options,
    StubHttpHandler tokenServer,
    StubHttpHandler scim,
    ScimTokenCache? cache = null) =>
    new(
      Options.Create(options),
      new StubHttpClientFactory().Add(ScimAuthenticationHandler.TokenClientName, tokenServer),
      cache ?? new ScimTokenCache())
    {
      InnerHandler = scim
    };

  private static HttpClient ClientThrough(ScimAuthenticationHandler handler) =>
    new(handler) { BaseAddress = new Uri("https://is.test/") };

  private static string Base64(string value) =>
    Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
}
