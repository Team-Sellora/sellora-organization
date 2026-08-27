using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sellora.CoreService.Application.Shops;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public sealed class ShopListEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public ShopListEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Get_TerritoryFilter_ReturnsOnlyShopsInOperatorsTerritory()
  {
    var northResponse = await GetAsync(
      $"?territoryId={HierarchyEndpointTestData.NorthTerritoryId}");

    Assert.Equal(HttpStatusCode.OK, northResponse.StatusCode);

    var northPage = await northResponse.Content
      .ReadFromJsonAsync<PagedResponse<ShopResponse>>();

    Assert.NotNull(northPage);
    Assert.NotEmpty(northPage.Items);
    Assert.All(
      northPage.Items,
      shop => Assert.Equal(
        HierarchyEndpointTestData.NorthTerritoryId,
        shop.TerritoryId));

    var southResponse = await GetAsync(
      $"?territoryId={HierarchyEndpointTestData.SouthTerritoryId}");

    Assert.Equal(HttpStatusCode.OK, southResponse.StatusCode);

    var southPage = await southResponse.Content
      .ReadFromJsonAsync<PagedResponse<ShopResponse>>();

    Assert.NotNull(southPage);
    Assert.Empty(southPage.Items);
    Assert.Equal(0, southPage.TotalCount);
  }

  [Fact]
  public async Task Get_StatusFilter_ReturnsInactiveShopOnlyInInactiveList()
  {

    using var scope = _factory.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var shop = await db.Shops
      .IgnoreQueryFilters()
      .SingleAsync(shop =>
        shop.ShopId == HierarchyEndpointTestData.NorthOtherShopId);

    shop.Status = "Inactive";

    await db.SaveChangesAsync();

    var inactiveResponse = await GetAsync(
      $"?territoryId={HierarchyEndpointTestData.NorthTerritoryId}&status=Inactive");

    Assert.Equal(HttpStatusCode.OK, inactiveResponse.StatusCode);

    var inactivePage = await inactiveResponse.Content
      .ReadFromJsonAsync<PagedResponse<ShopResponse>>();

    Assert.NotNull(inactivePage);

    // Add a separate test setup step before this request:
    // set NorthOtherShopId.Status = "Inactive" through CoreDbContext.
    Assert.All(
      inactivePage.Items,
      shop => Assert.Equal("Inactive", shop.Status));
  }

  private async Task<HttpResponseMessage> GetAsync(string query)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: "AgencyOperator",
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: HierarchyEndpointTestData.AgencyOperatorSubject);

    var request = new HttpRequestMessage(
      HttpMethod.Get,
      $"/api/shops{query}");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    return await _factory.CreateClient().SendAsync(request);
  }
}