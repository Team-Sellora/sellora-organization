using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using Xunit;

namespace Sellora.CoreService.Tests;

public sealed class TerritoryAgencyAssignmentEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public TerritoryAgencyAssignmentEndpointTests(TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Put_SameProvinceAgency_CreatesActiveAssignment()
  {
    var provinceId = SeedProvince("ASSIGN-OK");
    SeedManagerAssignment(
      provinceId,
      HierarchyEndpointTestData.AreaManagerSubject);

    var territoryId = SeedTerritory(provinceId, "ASSIGN-OK-T");
    var agencyId = SeedAgency(provinceId, "Assignment Test Agency");

    var response = await PutAsync(territoryId, agencyId);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var assignment = await db.TerritoryAgencyAssignments
      .IgnoreQueryFilters()
      .SingleAsync(item =>
        item.TerritoryId == territoryId &&
        item.EndsAt == null);

    Assert.Equal(agencyId, assignment.AgencyId);
    Assert.Equal(
      HierarchyEndpointTestData.CompanyId,
      assignment.CompanyId);
    Assert.Equal(
      HierarchyEndpointTestData.AreaManagerSubject,
      assignment.CreatedBy);
  }

  [Fact]
  public async Task Put_TerritoryOutsideCallerProvinces_ReturnsForbidden()
  {
    // The caller manages North, not South.
    var response = await PutAsync(
      HierarchyEndpointTestData.SouthTerritoryId,
      HierarchyEndpointTestData.SouthAgencyId);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains("territory", body, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("not in your provinces", body, StringComparison.OrdinalIgnoreCase);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var count = await db.TerritoryAgencyAssignments
      .IgnoreQueryFilters()
      .CountAsync(item =>
        item.TerritoryId == HierarchyEndpointTestData.SouthTerritoryId &&
        item.EndsAt == null);

    // Only the original seeded assignment remains; CSP-72 made no change.
    Assert.Equal(1, count);
  }

  [Fact]
  public async Task Put_AgencyOutsideCallerProvinces_ReturnsForbidden()
  {
    var provinceId = SeedProvince("AGENCY-SCOPE");
    SeedManagerAssignment(
      provinceId,
      HierarchyEndpointTestData.AreaManagerSubject);

    var territoryId = SeedTerritory(provinceId, "AGENCY-SCOPE-T");

    // South agency is outside the North Area Manager's managed provinces.
    var response = await PutAsync(
      territoryId,
      HierarchyEndpointTestData.SouthAgencyId);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains("agency", body, StringComparison.OrdinalIgnoreCase);
    Assert.Contains("not in your provinces", body, StringComparison.OrdinalIgnoreCase);

    await AssertUnassignedAsync(territoryId);
  }

  [Fact]
  public async Task Put_AgencyInDifferentManagedProvince_ReturnsForbidden()
  {
    var territoryProvinceId = SeedProvince("WEST-TEST");
    var agencyProvinceId = SeedProvince("SOUTH-TEST");

    // The same AM owns both provinces; this proves the separate
    // same-province rule, not merely the managed-province rule.
    SeedManagerAssignment(
      territoryProvinceId,
      HierarchyEndpointTestData.AreaManagerSubject);
    SeedManagerAssignment(
      agencyProvinceId,
      HierarchyEndpointTestData.AreaManagerSubject);

    var territoryId = SeedTerritory(
      territoryProvinceId,
      "WEST-TERRITORY");
    var agencyId = SeedAgency(
      agencyProvinceId,
      "Different Province Agency");

    var response = await PutAsync(territoryId, agencyId);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

    var body = await response.Content.ReadAsStringAsync();
    Assert.Contains("same province", body, StringComparison.OrdinalIgnoreCase);

    await AssertUnassignedAsync(territoryId);
  }

  [Fact]
  public async Task Put_NonAreaManagerCaller_ReturnsForbidden()
  {
    var response = await PutAsync(
      Guid.NewGuid(),
      Guid.NewGuid(),
      callerRole: Roles.AgencyOperator,
      callerSubject: HierarchyEndpointTestData.AgencyOperatorSubject);

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  private async Task<HttpResponseMessage> PutAsync(
    Guid territoryId,
    Guid agencyId,
    string callerRole = Roles.AreaManager,
    string callerSubject = HierarchyEndpointTestData.AreaManagerSubject)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: callerRole,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: callerSubject);

    var client = _factory.CreateClient();
    var request = new HttpRequestMessage(
      HttpMethod.Put,
      $"/api/territories/{territoryId}/agency");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    request.Content = JsonContent.Create(new
    {
      agencyId
    });

    return await client.SendAsync(request);
  }

  private Guid SeedProvince(string prefix)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var provinceId = Guid.NewGuid();

    db.Provinces.Add(new Province
    {
      ProvinceId = provinceId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      Code = $"{prefix}-{Guid.NewGuid():N}"[..20],
      Name = $"Province {prefix} {provinceId:N}"[..50],
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.SaveChanges();
    return provinceId;
  }

  private Guid SeedTerritory(Guid provinceId, string code)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var territoryId = Guid.NewGuid();

    db.Territories.Add(new Territory
    {
      TerritoryId = territoryId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      ProvinceId = provinceId,
      Code = $"{code}-{Guid.NewGuid():N}"[..40],
      Name = $"Territory {territoryId:N}",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.SaveChanges();
    return territoryId;
  }

  private Guid SeedAgency(Guid provinceId, string name)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var agencyId = Guid.NewGuid();

    db.Agencies.Add(new Agency
    {
      AgencyId = agencyId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      ProvinceId = provinceId,
      Name = $"{name} {agencyId:N}",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.SaveChanges();
    return agencyId;
  }

  private void SeedManagerAssignment(
    Guid provinceId,
    string managerSubject)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var managerId = db.StaffProfiles
      .IgnoreQueryFilters()
      .Single(profile =>
        profile.IdentitySub == managerSubject)
      .StaffProfileId;

    db.ProvinceManagerAssignments.Add(new ProvinceManagerAssignment
    {
      AssignmentId = Guid.NewGuid(),
      CompanyId = HierarchyEndpointTestData.CompanyId,
      ProvinceId = provinceId,
      AreaManagerId = managerId,
      StartsAt = DateTimeOffset.UtcNow,
      EndsAt = null,
      CreatedBy = "test-seed"
    });

    db.SaveChanges();
  }

  private async Task AssertUnassignedAsync(Guid territoryId)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var hasAssignment = await db.TerritoryAgencyAssignments
      .IgnoreQueryFilters()
      .AnyAsync(item =>
        item.TerritoryId == territoryId &&
        item.EndsAt == null);

    Assert.False(hasAssignment);
  }

  [Fact]
  public async Task Put_ReassigningTerritory_EndsOldAssignmentAndKeepsOneActive()
  {
    var provinceId = SeedProvince("REASSIGN");
    SeedManagerAssignment(
      provinceId,
      HierarchyEndpointTestData.AreaManagerSubject);

    var territoryId = SeedTerritory(provinceId, "REASSIGN-T");
    var firstAgencyId = SeedAgency(provinceId, "First Assignment Agency");
    var secondAgencyId = SeedAgency(provinceId, "Second Assignment Agency");

    var firstResponse = await PutAsync(territoryId, firstAgencyId);
    Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

    var secondResponse = await PutAsync(territoryId, secondAgencyId);
    Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var history = await db.TerritoryAgencyAssignments
      .IgnoreQueryFilters()
      .Where(assignment => assignment.TerritoryId == territoryId)
      .ToListAsync();

    Assert.Equal(2, history.Count);

    var oldAssignment = Assert.Single(
      history.Where(assignment => assignment.AgencyId == firstAgencyId));

    Assert.NotNull(oldAssignment.EndsAt);
    Assert.True(oldAssignment.EndsAt >= oldAssignment.StartsAt);

    var activeAssignment = Assert.Single(
      history.Where(assignment => assignment.EndsAt == null));

    Assert.Equal(secondAgencyId, activeAssignment.AgencyId);
    Assert.Equal(territoryId, activeAssignment.TerritoryId);
  }
}