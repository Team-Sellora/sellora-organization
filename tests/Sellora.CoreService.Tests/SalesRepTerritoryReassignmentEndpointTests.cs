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

public sealed class SalesRepTerritoryReassignmentEndpointTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public SalesRepTerritoryReassignmentEndpointTests(
    TestWebAppFactory factory)
  {
    _factory = factory;
    _factory.CreateClient();
  }

  [Fact]
  public async Task Put_ReassigningRep_EndsOldBindingAndKeepsOneActiveBinding()
  {
    var firstTerritoryId = SeedTerritoryAssignedToNorthAgency();
    var secondTerritoryId = SeedTerritoryAssignedToNorthAgency();
    var salesRepId = SeedSalesRep("Reassigned Sales Rep");

    Assert.Equal(
      HttpStatusCode.OK,
      (await PutAsync(firstTerritoryId, salesRepId)).StatusCode);

    var response = await PutAsync(secondTerritoryId, salesRepId);

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);

    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var history = await db.SalesRepTerritoryAssignments
      .IgnoreQueryFilters()
      .Where(assignment => assignment.SalesRepId == salesRepId)
      .ToListAsync();

    Assert.Equal(2, history.Count);

    var oldAssignment = Assert.Single(
      history.Where(assignment =>
        assignment.TerritoryId == firstTerritoryId));

    Assert.NotNull(oldAssignment.EndsAt);
    Assert.True(oldAssignment.EndsAt >= oldAssignment.StartsAt);

    var activeAssignment = Assert.Single(
      history.Where(assignment => assignment.EndsAt == null));

    Assert.Equal(secondTerritoryId, activeAssignment.TerritoryId);

    var activeRepBindings = await db.SalesRepTerritoryAssignments
      .IgnoreQueryFilters()
      .CountAsync(assignment =>
        assignment.SalesRepId == salesRepId &&
        assignment.EndsAt == null);

    var activeTerritoryBindings = await db.SalesRepTerritoryAssignments
      .IgnoreQueryFilters()
      .CountAsync(assignment =>
        assignment.TerritoryId == secondTerritoryId &&
        assignment.EndsAt == null);

    var oldTerritoryActiveBindings = await db.SalesRepTerritoryAssignments
      .IgnoreQueryFilters()
      .CountAsync(assignment =>
        assignment.TerritoryId == firstTerritoryId &&
        assignment.EndsAt == null);

    Assert.Equal(1, activeRepBindings);
    Assert.Equal(1, activeTerritoryBindings);
    Assert.Equal(0, oldTerritoryActiveBindings);
  }

  private async Task<HttpResponseMessage> PutAsync(
    Guid territoryId,
    Guid salesRepId)
  {
    var token = TestTokenFactory.CreateToken(
      _factory.Issuer,
      _factory.Audience,
      role: Roles.AgencyOperator,
      companyId: HierarchyEndpointTestData.CompanyId.ToString(),
      sub: HierarchyEndpointTestData.AgencyOperatorSubject);

    var request = new HttpRequestMessage(
      HttpMethod.Put,
      $"/api/territories/{territoryId}/sales-rep");

    request.Headers.Authorization =
      new AuthenticationHeaderValue("Bearer", token);

    request.Content = JsonContent.Create(new
    {
      salesRepId
    });

    return await _factory.CreateClient().SendAsync(request);
  }

  private Guid SeedTerritoryAssignedToNorthAgency()
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var territoryId = Guid.NewGuid();
    var now = DateTimeOffset.UtcNow;

    db.Territories.Add(new Territory
    {
      TerritoryId = territoryId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      ProvinceId = HierarchyEndpointTestData.NorthProvinceId,
      Code = $"REASSIGN-{territoryId:N}",
      Name = $"Reassignment Territory {territoryId:N}",
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
      EndsAt = null,
      CreatedBy = "test-seed"
    });

    db.SaveChanges();
    return territoryId;
  }

  private Guid SeedSalesRep(string displayName)
  {
    using var scope = _factory.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();

    var salesRepId = Guid.NewGuid();

    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = salesRepId,
      CompanyId = HierarchyEndpointTestData.CompanyId,
      IdentitySub = $"sales-rep-{salesRepId:N}",
      Role = Roles.SalesRep,
      DisplayName = displayName,
      Email = $"{salesRepId:N}@sellora.test",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.SaveChanges();
    return salesRepId;
  }
}