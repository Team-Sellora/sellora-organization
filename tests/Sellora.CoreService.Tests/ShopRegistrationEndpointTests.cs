using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Infrastructure.Persistence;
using Sellora.CoreService.Application.Hierarchy;

namespace Sellora.CoreService.Tests;

public sealed class ShopRegistrationEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public ShopRegistrationEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Post_TerritoryAssignedToOperatorsAgency_CreatesActiveShop()
  {
    var response = await RegisterAsync(
      subject: HierarchyEndpointTestData.AgencyOperatorSubject,
      territoryId: HierarchyEndpointTestData.NorthTerritoryId,
      name: "New North Shop");

    Assert.Equal(HttpStatusCode.Created, response.StatusCode);

    using var document = JsonDocument.Parse(
      await response.Content.ReadAsStringAsync());

    var shopId = document.RootElement
      .GetProperty("shopId")
      .GetGuid();

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var shop = await db.Shops
      .IgnoreQueryFilters()
      .SingleAsync(candidate => candidate.ShopId == shopId);

    Assert.Equal(HierarchyEndpointTestData.CompanyId, shop.CompanyId);
    Assert.Equal(HierarchyEndpointTestData.NorthTerritoryId, shop.TerritoryId);
    Assert.Equal("New North Shop", shop.Name);
    Assert.Equal("Active", shop.Status);

    await OutboxEventAssertions.AssertEventAsync(
      db,
      "ShopRegistered",
      HierarchyEndpointTestData.CompanyId,
      shopId,
      ("shopId", shopId),
      ("agencyId", HierarchyEndpointTestData.NorthAgencyId),
      ("territoryId", HierarchyEndpointTestData.NorthTerritoryId));
  }

  [Fact]
  public async Task Post_TerritoryAssignedToAnotherAgency_ReturnsForbiddenAndCreatesNothing()
  {
    using var beforeScope = _factory.Services.CreateScope();
    var beforeDb = beforeScope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var shopCountBefore = await beforeDb.Shops
      .IgnoreQueryFilters()
      .CountAsync();

    var response = await RegisterAsync(
      subject: HierarchyEndpointTestData.AgencyOperatorSubject,
      territoryId: HierarchyEndpointTestData.SouthTerritoryId,
      name: "Forbidden South Shop");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

    Assert.NotNull(problem);
    Assert.Contains(
      "not currently assigned to your agency",
      problem.Detail!,
      StringComparison.OrdinalIgnoreCase);

    using var afterScope = _factory.Services.CreateScope();
    var afterDb = afterScope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var shopCountAfter = await afterDb.Shops
      .IgnoreQueryFilters()
      .CountAsync();

    Assert.Equal(shopCountBefore, shopCountAfter);
  }

  private async Task<HttpResponseMessage> RegisterAsync(
    string subject,
    Guid territoryId,
    string name,
    string? ownerIdentitySub = null)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: "AgencyOperator",
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: subject);

    var request = new HttpRequestMessage(HttpMethod.Post, "/api/shops");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    ownerIdentitySub ??= $"shop-owner-{Guid.NewGuid():N}";

    request.Content = JsonContent.Create(new
    {
      territoryId,
      name,
      ownerName = "Test Shop Owner",
      ownerIdentitySub,
      ownerEmail = "owner@test.local",
      ownerPhone = "0771234567",
      address = "123 Test Road",
      latitude = 6.927079m,
      longitude = 79.861244m,
      creditLimit = 10000m
    });

    return await _factory.CreateClient().SendAsync(request);
  }

  [Fact]
  public async Task Post_MissingLatitude_ReturnsBadRequestAndCreatesNothing()
  {
    var shopCountBefore = await CountShopsAsync();

    var response = await SendRegistrationAsync(new
    {
      territoryId = HierarchyEndpointTestData.NorthTerritoryId,
      name = "No GPS Shop",
      ownerName = "Test Shop Owner",
      ownerEmail = "owner@test.local",
      ownerPhone = "0771234567",
      address = "123 Test Road",
      longitude = 79.861244m,
      creditLimit = 10000m
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

    Assert.NotNull(problem);
    Assert.Contains(
      "latitude is required",
      problem.Detail!,
      StringComparison.OrdinalIgnoreCase);

    Assert.Equal(shopCountBefore, await CountShopsAsync());
  }

  [Theory]
  [InlineData(0.0, 0.0)]
  [InlineData(51.5072, -0.1276)]
  public async Task Post_CoordinatesOutsideSriLanka_ReturnsBadRequest(
    decimal latitude,
    decimal longitude)
  {
    var response = await SendRegistrationAsync(new
    {
      territoryId = HierarchyEndpointTestData.NorthTerritoryId,
      name = $"Invalid GPS Shop {latitude}",
      ownerName = "Test Shop Owner",
      ownerEmail = "owner@test.local",
      ownerPhone = "0771234567",
      address = "123 Test Road",
      latitude,
      longitude,
      creditLimit = 10000m
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

    Assert.NotNull(problem);
    Assert.Contains(
      "within Sri Lanka",
      problem.Detail!,
      StringComparison.OrdinalIgnoreCase);
  }

  [Theory]
  [InlineData(0.0)]
  [InlineData(-1.0)]
  public async Task Post_NonPositiveCreditLimit_ReturnsBadRequest(
    decimal creditLimit)
  {
    var shopCountBefore = await CountShopsAsync();

    var response = await SendRegistrationAsync(new
    {
      territoryId = HierarchyEndpointTestData.NorthTerritoryId,
      name = $"Invalid Credit Shop {creditLimit}",
      ownerName = "Test Shop Owner",
      ownerEmail = "owner@test.local",
      ownerPhone = "0771234567",
      address = "123 Test Road",
      latitude = 6.927079m,
      longitude = 79.861244m,
      creditLimit
    });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

    var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();

    Assert.NotNull(problem);
    Assert.Contains(
      "must be greater than zero",
      problem.Detail!,
      StringComparison.OrdinalIgnoreCase);

    Assert.Equal(shopCountBefore, await CountShopsAsync());
  }

  private async Task<HttpResponseMessage> SendRegistrationAsync(object body)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: "AgencyOperator",
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: HierarchyEndpointTestData.AgencyOperatorSubject);

    var request = new HttpRequestMessage(HttpMethod.Post, "/api/shops");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    request.Content = JsonContent.Create(body);

    return await _factory.CreateClient().SendAsync(request);
  }

  private async Task<int> CountShopsAsync()
  {
    using var scope = _factory.Services.CreateScope();

    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    return await db.Shops
      .IgnoreQueryFilters()
      .CountAsync();
  }

  [Fact]
  public async Task Post_LinkedOwnerIdentity_AllowsShopOwnerTokenToResolveNewShop()
  {
    var ownerIdentitySub = $"shop-owner-{Guid.NewGuid():N}";

    var registrationResponse = await RegisterAsync(
      subject: HierarchyEndpointTestData.AgencyOperatorSubject,
      territoryId: HierarchyEndpointTestData.NorthTerritoryId,
      name: "Owner Linked Shop",
      ownerIdentitySub: ownerIdentitySub);

    Assert.Equal(HttpStatusCode.Created, registrationResponse.StatusCode);

    using var registrationDocument = JsonDocument.Parse(
      await registrationResponse.Content.ReadAsStringAsync());

    var shopId = registrationDocument.RootElement
      .GetProperty("shopId")
      .GetGuid();

    var shopOwnerToken = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: "ShopOwner",
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: ownerIdentitySub);

    var hierarchyRequest = new HttpRequestMessage(
      HttpMethod.Get,
      "/api/hierarchy");

    hierarchyRequest.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", shopOwnerToken);

    var hierarchyResponse = await _factory
      .CreateClient()
      .SendAsync(hierarchyRequest);

    Assert.Equal(HttpStatusCode.OK, hierarchyResponse.StatusCode);

    var hierarchy = await hierarchyResponse.Content
      .ReadFromJsonAsync<HierarchyTreeResponse>();

    Assert.NotNull(hierarchy);

    var visibleShops = hierarchy.Provinces
      .SelectMany(province => province.Agencies)
      .SelectMany(agency => agency.Territories)
      .SelectMany(territory => territory.Shops)
      .ToList();

    var visibleShop = Assert.Single(visibleShops);

    Assert.Equal(shopId, visibleShop.ShopId);
  }
}