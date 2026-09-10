using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sellora.CoreService.Application.Hierarchy;

namespace Sellora.CoreService.Tests;

public sealed class HierarchyEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public HierarchyEndpointTests(
    TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task CompanyAdmin_SeesEntireCompany()
  {
    var hierarchy = await GetHierarchyAsync(
      "CompanyAdmin",
      "company-admin");

    Assert.Equal(
      HierarchyEndpointTestData.CompanyId,
      hierarchy.CompanyId);

    Assert.Equal(
      new[]
      {
        HierarchyEndpointTestData.NorthProvinceId,
        HierarchyEndpointTestData.SouthProvinceId
      }.OrderBy(id => id),
      hierarchy.Provinces
        .Select(province => province.ProvinceId)
        .OrderBy(id => id));

    Assert.DoesNotContain(
      hierarchy.Provinces,
      province =>
        province.ProvinceId ==
        HierarchyEndpointTestData.OtherProvinceId);
  }

  [Fact]
  public async Task AreaManager_SeesOnlyManagedProvinces()
  {
    var hierarchy = await GetHierarchyAsync(
      "AreaManager",
      HierarchyEndpointTestData.AreaManagerSubject);

    var province = Assert.Single(hierarchy.Provinces);

    Assert.Equal(
      HierarchyEndpointTestData.NorthProvinceId,
      province.ProvinceId);

    var agency = Assert.Single(province.Agencies);

    Assert.Equal(
      HierarchyEndpointTestData.NorthAgencyId,
      agency.AgencyId);
  }

  [Fact]
  public async Task AgencyOperator_SeesOnlyAssignedAgency()
  {
    var hierarchy = await GetHierarchyAsync(
      "AgencyOperator",
      HierarchyEndpointTestData.AgencyOperatorSubject);

    var province = Assert.Single(hierarchy.Provinces);
    var agency = Assert.Single(province.Agencies);
    var territory = Assert.Single(agency.Territories);

    Assert.Equal(
      HierarchyEndpointTestData.NorthAgencyId,
      agency.AgencyId);

    Assert.Equal(
      HierarchyEndpointTestData.NorthTerritoryId,
      territory.TerritoryId);

    Assert.Equal(2, territory.Shops.Count);
  }

  [Fact]
  public async Task SalesRep_SeesOnlyAssignedTerritory()
  {
    var hierarchy = await GetHierarchyAsync(
      "SalesRep",
      HierarchyEndpointTestData.SalesRepSubject);

    var province = Assert.Single(hierarchy.Provinces);
    var agency = Assert.Single(province.Agencies);
    var territory = Assert.Single(agency.Territories);

    Assert.Equal(
      HierarchyEndpointTestData.NorthTerritoryId,
      territory.TerritoryId);

    Assert.DoesNotContain(
      FlattenTerritories(hierarchy),
      candidate =>
        candidate.TerritoryId ==
        HierarchyEndpointTestData.SouthTerritoryId);
  }

  [Fact]
  public async Task ShopOwner_SeesOnlyOwnShop()
  {
    var hierarchy = await GetHierarchyAsync(
      "ShopOwner",
      HierarchyEndpointTestData.ShopOwnerSubject);

    var shops = FlattenTerritories(hierarchy)
      .SelectMany(territory => territory.Shops)
      .ToList();

    var shop = Assert.Single(shops);

    Assert.Equal(
      HierarchyEndpointTestData.NorthShopId,
      shop.ShopId);
  }

  private async Task<HierarchyTreeResponse>
    GetHierarchyAsync(
      string role,
      string subject)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role,
      companyId:
        HierarchyEndpointTestData.CompanyId.ToString(),
      sub: subject);

    var request = new HttpRequestMessage(
      HttpMethod.Get,
      "/api/hierarchy");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    var response = await _factory
      .CreateClient()
      .SendAsync(request);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    return await response.Content
      .ReadFromJsonAsync<HierarchyTreeResponse>()
      ?? throw new InvalidOperationException(
        "The hierarchy response body was empty.");
  }

  private static IEnumerable<TerritoryHierarchyNode>
    FlattenTerritories(HierarchyTreeResponse hierarchy)
  {
    return hierarchy.Provinces.SelectMany(
      province =>
        province.Agencies
          .SelectMany(agency => agency.Territories)
          .Concat(province.UnassignedTerritories));
  }
}