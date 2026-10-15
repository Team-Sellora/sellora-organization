using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence.Seeding;

namespace Sellora.CoreService.Tests;

/// <summary>
/// The staging demo seed, checked without Docker. The PostgreSQL variant in
/// DevelopmentOrganizationSeederTests additionally proves it satisfies the
/// real constraints.
/// </summary>
[Trait("Category", "Integration")]
public sealed class DevelopmentOrganizationSeederSqliteTests : IDisposable
{
  private readonly SqliteCoreDatabase _database = new();

  public void Dispose() => _database.Dispose();

  [Fact]
  public async Task Seed_CreatesTheDemoHierarchyOnce()
  {
    await using (var db = _database.CreateDbContext())
    {
      await DevelopmentOrganizationSeeder.SeedAsync(db);
    }

    await using (var db = _database.CreateDbContext())
    {
      // A restart must not duplicate anything.
      await DevelopmentOrganizationSeeder.SeedAsync(db);
    }

    await using var verify = _database.CreateDbContext(DevelopmentOrganizationSeedIds.CompanyId);

    (await verify.Companies.IgnoreQueryFilters().CountAsync()).Should().Be(1);
    (await verify.Provinces.CountAsync()).Should().Be(2);
    (await verify.Agencies.CountAsync()).Should().Be(2);
    (await verify.Territories.CountAsync()).Should().Be(2);
    (await verify.Shops.CountAsync()).Should().Be(2);
    (await verify.StaffProfiles.CountAsync()).Should().Be(7);
    (await verify.ProvinceManagerAssignments.CountAsync(a => a.EndsAt == null)).Should().Be(2);
    (await verify.AgencyOperatorAssignments.CountAsync(a => a.EndsAt == null)).Should().Be(2);
    (await verify.TerritoryAgencyAssignments.CountAsync(a => a.EndsAt == null)).Should().Be(2);
    (await verify.SalesRepTerritoryAssignments.CountAsync(a => a.EndsAt == null)).Should().Be(2);

    var provinceNames = await verify.Provinces.Select(province => province.Name).ToListAsync();
    provinceNames.Should().BeEquivalentTo("Western Province", "Central Province");

    (await verify.Shops.AllAsync(shop => shop.Status == HierarchyStatus.Active)).Should().BeTrue();
    (await verify.SalesRepTerritoryAssignments.AllAsync(a => a.CreatedBy == "development-seed"))
      .Should().BeTrue();
  }
}
