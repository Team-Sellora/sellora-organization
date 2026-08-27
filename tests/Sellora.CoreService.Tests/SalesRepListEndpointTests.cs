using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public sealed class SalesRepListEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public SalesRepListEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Get_OnlyReturnsRepsVisibleToOperatorsAgency()
  {
    var response = await GetAsync("/api/sales-reps");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var reps = await response.Content
      .ReadFromJsonAsync<List<SalesRepSummary>>();

    Assert.NotNull(reps);

    var northRep = Assert.Single(reps, rep =>
      rep.CurrentTerritory?.TerritoryId ==
      HierarchyEndpointTestData.NorthTerritoryId);

    Assert.Equal(HierarchyEndpointTestData.NorthTerritoryId,
      northRep.CurrentTerritory!.TerritoryId);

    Assert.DoesNotContain(reps, rep =>
      rep.DisplayName == "sales-rep-south");
  }

  [Fact]
  public async Task Get_UnassignedTerritories_ReturnsOnlyUnassignedTerritoriesInOperatorsAgency()
  {
    var unassignedTerritoryId = SeedUnassignedNorthTerritory();

    var response = await GetAsync(
      "/api/sales-reps/unassigned-territories");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    var territories = await response.Content
      .ReadFromJsonAsync<List<TerritoryResponse>>();

    Assert.NotNull(territories);

    Assert.Contains(territories, territory =>
      territory.TerritoryId == unassignedTerritoryId);

    // Already assigned to a rep, so it must not appear.
    Assert.DoesNotContain(territories, territory =>
      territory.TerritoryId ==
      HierarchyEndpointTestData.NorthTerritoryId);

    // Belongs to another agency, so it must not appear.
    Assert.DoesNotContain(territories, territory =>
      territory.TerritoryId ==
      HierarchyEndpointTestData.SouthTerritoryId);
  }

  private Guid SeedUnassignedNorthTerritory()
  {
    using var scope = _factory.Services.CreateScope();

    var db = scope.ServiceProvider
      .GetRequiredService<CoreDbContext>();

    var territoryId = Guid.NewGuid();
    var now = DateTimeOffset.UtcNow;

    db.Territories.Add(new Territory
    {
      TerritoryId = territoryId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      ProvinceId = HierarchyEndpointTestData.NorthProvinceId,
      Code = $"UNASSIGNED-{territoryId:N}",
      Name = $"Unassigned Territory {territoryId:N}",
      Status = HierarchyStatus.Active,
      CreatedAt = now
    });

    db.TerritoryAgencyAssignments.Add(new TerritoryAgencyAssignment
    {
      AssignmentId = Guid.NewGuid(),
      CompanyId = HierarchyEndpointTestData.CompanyId,
      TerritoryId = territoryId,
      AgencyId = HierarchyEndpointTestData.NorthAgencyId,
      StartsAt = now,
      CreatedBy = "test-seed"
    });

    db.SaveChanges();

    return territoryId;
  }

  private async Task<HttpResponseMessage> GetAsync(string path)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: Roles.AgencyOperator,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: HierarchyEndpointTestData.AgencyOperatorSubject);

    var request = new HttpRequestMessage(HttpMethod.Get, path);

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    return await _factory.CreateClient().SendAsync(request);
  }
}