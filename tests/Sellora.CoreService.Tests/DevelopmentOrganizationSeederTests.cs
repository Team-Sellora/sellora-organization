using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence.Seeding;

namespace Sellora.CoreService.Tests;

public sealed class DevelopmentOrganizationSeederTests
  : IClassFixture<PostgreSqlConstraintFixture>
{
  private readonly PostgreSqlConstraintFixture _fixture;

  public DevelopmentOrganizationSeederTests(
    PostgreSqlConstraintFixture fixture)
  {
    _fixture = fixture;
  }

  [Fact]
  public async Task Seed_CreatesTwoProvinceHierarchy_AndIsIdempotent()
  {
    await using var db = _fixture.CreateDbContext();

    await DevelopmentOrganizationSeeder.SeedAsync(db);

    // Repeating startup must not duplicate any records.
    await DevelopmentOrganizationSeeder.SeedAsync(db);

    var companyId =
      DevelopmentOrganizationSeedIds.CompanyId;

    var companies = await db.Companies
      .IgnoreQueryFilters()
      .Where(company =>
        company.CompanyId == companyId)
      .ToListAsync();

    Assert.Single(companies);

    var provinces = await db.Provinces
      .IgnoreQueryFilters()
      .Where(province =>
        province.CompanyId == companyId)
      .ToListAsync();

    Assert.Equal(2, provinces.Count);

    Assert.Contains(
      provinces,
      province =>
        province.ProvinceId ==
          DevelopmentOrganizationSeedIds
            .WesternProvinceId &&
        province.Name == "Western Province");

    Assert.Contains(
      provinces,
      province =>
        province.ProvinceId ==
          DevelopmentOrganizationSeedIds
            .CentralProvinceId &&
        province.Name == "Central Province");

    var agencyCount = await db.Agencies
      .IgnoreQueryFilters()
      .CountAsync(agency =>
        agency.CompanyId == companyId);

    var territoryCount = await db.Territories
      .IgnoreQueryFilters()
      .CountAsync(territory =>
        territory.CompanyId == companyId);

    var shopCount = await db.Shops
      .IgnoreQueryFilters()
      .CountAsync(shop =>
        shop.CompanyId == companyId);

    var staffCount = await db.StaffProfiles
      .IgnoreQueryFilters()
      .CountAsync(profile =>
        profile.CompanyId == companyId);

    var managerAssignmentCount =
      await db.ProvinceManagerAssignments
        .IgnoreQueryFilters()
        .CountAsync(assignment =>
          assignment.CompanyId == companyId &&
          assignment.EndsAt == null);

    var operatorAssignmentCount =
      await db.AgencyOperatorAssignments
        .IgnoreQueryFilters()
        .CountAsync(assignment =>
          assignment.CompanyId == companyId &&
          assignment.EndsAt == null);

    var agencyAssignmentCount =
      await db.TerritoryAgencyAssignments
        .IgnoreQueryFilters()
        .CountAsync(assignment =>
          assignment.CompanyId == companyId &&
          assignment.EndsAt == null);

    var repAssignmentCount =
      await db.SalesRepTerritoryAssignments
        .IgnoreQueryFilters()
        .CountAsync(assignment =>
          assignment.CompanyId == companyId &&
          assignment.EndsAt == null);

    Assert.Equal(2, agencyCount);
    Assert.Equal(2, territoryCount);
    Assert.Equal(2, shopCount);
    Assert.Equal(7, staffCount);
    Assert.Equal(2, managerAssignmentCount);
    Assert.Equal(2, operatorAssignmentCount);
    Assert.Equal(2, agencyAssignmentCount);
    Assert.Equal(2, repAssignmentCount);

    var allHierarchyRowsActive =
      await db.Provinces
        .IgnoreQueryFilters()
        .Where(province =>
          province.CompanyId == companyId)
        .AllAsync(province =>
          province.Status == HierarchyStatus.Active) &&
      await db.Agencies
        .IgnoreQueryFilters()
        .Where(agency =>
          agency.CompanyId == companyId)
        .AllAsync(agency =>
          agency.Status == HierarchyStatus.Active) &&
      await db.Territories
        .IgnoreQueryFilters()
        .Where(territory =>
          territory.CompanyId == companyId)
        .AllAsync(territory =>
          territory.Status == HierarchyStatus.Active) &&
      await db.Shops
        .IgnoreQueryFilters()
        .Where(shop =>
          shop.CompanyId == companyId)
        .AllAsync(shop =>
          shop.Status == HierarchyStatus.Active);

    Assert.True(allHierarchyRowsActive);
  }
}