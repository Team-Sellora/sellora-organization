using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Verifies the named role authorization policies: the correct role is admitted
/// and any other role is forbidden at the endpoint.
/// </summary>
public class RolePolicyTests : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public RolePolicyTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task SalesRepEndpoint_WithSalesRepToken_Returns200()
  {
    var token = TestTokenFactory.CreateToken(
        _factory.Issuer, _factory.Audience, role: "SalesRep");

    var response = await SendWithToken("/api/demo/sales-rep", token);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task SalesRepEndpoint_WithWrongRoleToken_Returns403()
  {
    var token = TestTokenFactory.CreateToken(
        _factory.Issuer, _factory.Audience, role: "CompanyAdmin");

    var response = await SendWithToken("/api/demo/sales-rep", token);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<HttpResponseMessage> SendWithToken(string path, string token)
  {
    var client = _factory.CreateClient();
    var request = new HttpRequestMessage(HttpMethod.Get, path);
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    return await client.SendAsync(request);
  }
}