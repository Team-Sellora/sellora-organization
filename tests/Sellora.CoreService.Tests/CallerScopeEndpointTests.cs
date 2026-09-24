using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sellora.CoreService.Application.Me;
using Sellora.CoreService.Domain.Identity;

namespace Sellora.CoreService.Tests;

/// <summary>GET /api/me/scope — the caller's position, from their sub.</summary>
public sealed class CallerScopeEndpointTests : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public CallerScopeEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Sales_rep_gets_their_own_id_territory_and_agency()
  {
    var scope = await GetScopeAsync(Roles.SalesRep, HierarchyEndpointTestData.SalesRepSubject);

    Assert.Equal(Roles.SalesRep, scope.Role);
    Assert.NotNull(scope.SalesRepId);
    Assert.Equal(scope.StaffProfileId, scope.SalesRepId);
    Assert.Equal(HierarchyEndpointTestData.NorthTerritoryId, scope.TerritoryId);
    Assert.Equal(HierarchyEndpointTestData.NorthAgencyId, scope.AgencyId);
    Assert.Contains(HierarchyEndpointTestData.NorthProvinceId, scope.ProvinceIds);
    Assert.Equal(HierarchyEndpointTestData.CompanyId, scope.CompanyId);
  }

  [Fact]
  public async Task Agency_operator_gets_their_agency_and_no_rep_id()
  {
    var scope = await GetScopeAsync(Roles.AgencyOperator, HierarchyEndpointTestData.AgencyOperatorSubject);

    Assert.Equal(HierarchyEndpointTestData.NorthAgencyId, scope.AgencyId);
    Assert.Null(scope.SalesRepId);
    Assert.Contains(HierarchyEndpointTestData.NorthTerritoryId, scope.TerritoryIds);
  }

  [Fact]
  public async Task Area_manager_gets_their_provinces()
  {
    var scope = await GetScopeAsync(Roles.AreaManager, HierarchyEndpointTestData.AreaManagerSubject);

    Assert.Equal(new[] { HierarchyEndpointTestData.NorthProvinceId }, scope.ProvinceIds);
    Assert.Null(scope.AgencyId);
  }

  [Fact]
  public async Task Shop_owner_gets_their_shop()
  {
    var scope = await GetScopeAsync(Roles.ShopOwner, HierarchyEndpointTestData.ShopOwnerSubject);

    Assert.Equal(HierarchyEndpointTestData.NorthShopId, scope.ShopId);
    Assert.Equal(HierarchyEndpointTestData.NorthAgencyId, scope.AgencyId);
  }

  [Fact]
  public async Task Company_admin_is_unrestricted()
  {
    var scope = await GetScopeAsync(Roles.CompanyAdmin, HierarchyEndpointTestData.CompanyAdminSubject);

    Assert.Equal(Roles.CompanyAdmin, scope.Role);
    Assert.Empty(scope.ProvinceIds);
    Assert.Null(scope.SalesRepId);
  }

  [Fact]
  public async Task A_login_with_no_profile_is_404_not_an_empty_scope()
  {
    var response = await SendAsync(Roles.SalesRep, "someone-never-registered");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  [Fact]
  public async Task A_profile_in_another_company_is_not_found()
  {
    var response = await SendAsync(
      Roles.SalesRep,
      HierarchyEndpointTestData.SalesRepSubject,
      HierarchyEndpointTestData.OtherCompanyId);

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
  }

  private async Task<CallerScopeResponse> GetScopeAsync(string role, string subject)
  {
    var response = await SendAsync(role, subject);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    return (await response.Content.ReadFromJsonAsync<CallerScopeResponse>())!;
  }

  private Task<HttpResponseMessage> SendAsync(string role, string subject, Guid? companyId = null)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: role,
      companyId: (companyId ?? HierarchyEndpointTestData.CompanyId).ToString(),
      sub: subject);

    var request = new HttpRequestMessage(HttpMethod.Get, "/api/me/scope");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

    return _factory.CreateClient().SendAsync(request);
  }
}
