using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sellora.CoreService.Application.CompanyAdmins;
using Sellora.CoreService.Domain.Identity;

namespace Sellora.CoreService.Tests;

public sealed class CompanyAdminListEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public CompanyAdminListEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Get_ReturnsActiveCompanyAdminsInCallersCompany()
  {
    var response = await GetAsync(Roles.CompanyAdmin);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var admins = await response.Content
      .ReadFromJsonAsync<IReadOnlyList<CompanyAdminSummary>>();

    var admin = Assert.Single(admins!);
    Assert.Equal(HierarchyEndpointTestData.CompanyAdminId, admin.StaffProfileId);
    Assert.Equal(HierarchyEndpointTestData.CompanyAdminSubject, admin.DisplayName);
  }

  [Fact]
  public async Task Get_NonAdminCaller_ReturnsForbidden()
  {
    var response = await GetAsync(Roles.AreaManager);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<HttpResponseMessage> GetAsync(string role)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role,
      HierarchyEndpointTestData.CompanyId.ToString(),
      HierarchyEndpointTestData.CompanyAdminSubject);

    var request = new HttpRequestMessage(HttpMethod.Get, "/api/company-admins");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    return await _factory.CreateClient().SendAsync(request);
  }
}
