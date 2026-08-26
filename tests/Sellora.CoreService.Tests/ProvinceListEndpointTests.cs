using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Application.Provinces;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using Xunit;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Verifies GET /api/provinces returns per-province manager info and correct
/// active agency/shop counts, scoped to the caller's company.
/// The counts are asserted against the actual seeded data.
/// </summary>
public sealed class ProvinceListEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public ProvinceListEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  // Primary AC — counts and current manager match seeded data
  [Fact]
  public async Task Get_CompanyAdmin_ReturnsCorrectCountsAndManagers()
  {
    var response = await GetAsync(
      callerRole: Roles.CompanyAdmin,
      companyId: HierarchyEndpointTestData.CompanyId.ToString());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var provinces = await response.Content
      .ReadFromJsonAsync<List<ProvinceSummaryResponse>>();
    Assert.NotNull(provinces);

    // Company A has exactly two provinces in the seed (North + South).
    // The OtherCompany province must not leak into the caller's list.
    Assert.Equal(2, provinces!.Count);
    Assert.DoesNotContain(
      provinces,
      p => p.ProvinceId == HierarchyEndpointTestData.OtherProvinceId);

    var north = provinces.Single(p =>
      p.ProvinceId == HierarchyEndpointTestData.NorthProvinceId);
    var south = provinces.Single(p =>
      p.ProvinceId == HierarchyEndpointTestData.SouthProvinceId);

    // North: 1 agency, 2 shops (NorthShop + NorthOtherShop), active manager.
    Assert.Equal(1, north.AgencyCount);
    Assert.Equal(2, north.ShopCount);
    Assert.NotNull(north.CurrentManager);
    Assert.Equal(
      HierarchyEndpointTestData.AreaManagerSubject,
      north.CurrentManager!.DisplayName);

    // South: 1 agency, 1 shop, active manager.
    Assert.Equal(1, south.AgencyCount);
    Assert.Equal(1, south.ShopCount);
    Assert.NotNull(south.CurrentManager);
  }

  // Cross-tenant + no-manager combined: Company B has one bare province
  [Fact]
  public async Task Get_CompanyBAdmin_SeesOnlyOwnProvinceWithNoManagerAndZeroCounts()
  {
    var response = await GetAsync(
      callerRole: Roles.CompanyAdmin,
      companyId: HierarchyEndpointTestData.OtherCompanyId.ToString());

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var provinces = await response.Content
      .ReadFromJsonAsync<List<ProvinceSummaryResponse>>();
    Assert.NotNull(provinces);

    var only = Assert.Single(provinces!);
    Assert.Equal(HierarchyEndpointTestData.OtherProvinceId, only.ProvinceId);
    Assert.Null(only.CurrentManager);   // seed did not assign one
    Assert.Equal(0, only.AgencyCount);
    Assert.Equal(0, only.ShopCount);
  }

  // Endpoint policy — non-admin caller refused
  [Fact]
  public async Task Get_NonAdminCaller_ReturnsForbidden()
  {
    var response = await GetAsync(
      callerRole: Roles.AreaManager,
      companyId: HierarchyEndpointTestData.CompanyId.ToString());

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  // Endpoint policy — no token refused
  [Fact]
  public async Task Get_NoToken_ReturnsUnauthorized()
  {
    var client = _factory.CreateClient();
    var response = await client.GetAsync("/api/provinces");
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  // ---------- helpers ----------

  private async Task<HttpResponseMessage> GetAsync(
    string callerRole,
    string companyId)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: callerRole,
      companyId: companyId,
      sub: "test-admin");

    var client = _factory.CreateClient();
    var request = new HttpRequestMessage(
      HttpMethod.Get,
      "/api/provinces");
    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    return await client.SendAsync(request);
  }
}