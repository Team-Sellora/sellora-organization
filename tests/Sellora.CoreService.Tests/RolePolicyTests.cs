using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Verifies the named role authorization policies: the correct role is admitted
/// and any other role is forbidden at the endpoint.
/// </summary>
/// <remarks>
/// <para>
/// Characteristic and blocks: the role in a valid token sent to the SalesRep
/// endpoint. The policy's role (SalesRep) / any other role (CompanyAdmin).
/// </para>
/// <para>
/// Boundary values: none numeric.
/// </para>
/// <para>
/// Coverage criterion: Each Choice, 2 rows. The 200 row can only pass if the
/// raw "roles" claim reaches the policy end to end, which is what keeps
/// MapInboundClaims = false honest in an integration test.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public class RolePolicyTests : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public RolePolicyTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Theory]
  [InlineData("SalesRep", HttpStatusCode.OK)] // Role in the policy
  [InlineData("CompanyAdmin", HttpStatusCode.Forbidden)] // Any other role
  public async Task SalesRepEndpoint_WithRoleToken_ReturnsExpectedStatus(
    string role,
    HttpStatusCode expectedStatus)
  {
    // Arrange
    var token = TestTokenFactory.CreateToken(
        _factory.Issuer, _factory.Audience, role: role);

    // Act
    var response = await SendWithToken("/api/demo/sales-rep", token);

    // Assert
    Assert.Equal(expectedStatus, response.StatusCode);
  }

  private async Task<HttpResponseMessage> SendWithToken(string path, string token)
  {
    var client = _factory.CreateClient();
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await client.SendAsync(request);
  }
}