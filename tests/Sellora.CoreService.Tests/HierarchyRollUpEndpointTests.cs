using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public sealed class HierarchyRollUpEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public HierarchyRollUpEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Get_ReturnsAccurateCountsAndFlagsUnassignedTerritories()
  {
    SeedUnassignedNorthTerritory();

    var response = await GetAsync(
      Roles.CompanyAdmin,
      HierarchyEndpointTestData.CompanyAdminSubject);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var rollUp = await response.Content
      .ReadFromJsonAsync<IReadOnlyList<ProvinceRollUpResponse>>();

    Assert.NotNull(rollUp);
    Assert.Equal(2, rollUp.Count);

    var north = Assert.Single(rollUp, province =>
      province.ProvinceId == HierarchyEndpointTestData.NorthProvinceId);

    Assert.NotNull(north.CurrentManager);
    Assert.NotNull(north.CurrentManager.ReportsToAdmin);
    Assert.Equal(
      HierarchyEndpointTestData.CompanyAdminId,
      north.CurrentManager.ReportsToAdmin.StaffProfileId);
    Assert.Equal(1, north.AgencyCount);
    Assert.Equal(2, north.TerritoryCount);
    Assert.Equal(2, north.ShopCount);
    Assert.Equal(1, north.UnassignedTerritoryCount);
    Assert.True(north.HasUnassignedTerritories);

    var south = Assert.Single(rollUp, province =>
      province.ProvinceId == HierarchyEndpointTestData.SouthProvinceId);

    Assert.Equal(1, south.AgencyCount);
    Assert.Equal(1, south.TerritoryCount);
    Assert.Equal(1, south.ShopCount);
    Assert.Equal(0, south.UnassignedTerritoryCount);
    Assert.False(south.HasUnassignedTerritories);
  }

  [Fact]
  public async Task Get_NonAdminCaller_ReturnsForbidden()
  {
    var response = await GetAsync(
      Roles.AreaManager,
      HierarchyEndpointTestData.CompanyAdminSubject);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<HttpResponseMessage> GetAsync(
    string role,
    string subject)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role,
      HierarchyEndpointTestData.CompanyId.ToString(),
      subject);

    var request = new HttpRequestMessage(HttpMethod.Get, "/api/hierarchy/roll-up");
    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    return await _factory.CreateClient().SendAsync(request);
  }

  private void SeedUnassignedNorthTerritory()
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    db.Territories.Add(new Territory
    {
      TerritoryId = Guid.NewGuid(),
      CompanyId = HierarchyEndpointTestData.CompanyId,
      ProvinceId = HierarchyEndpointTestData.NorthProvinceId,
      Code = $"UN{Guid.NewGuid():N}"[..12],
      Name = $"Unassigned {Guid.NewGuid():N}"[..20],
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.SaveChanges();
  }
}
