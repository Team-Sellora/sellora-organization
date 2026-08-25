using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Verifies hierarchy foreign keys and non-null requirements directly
/// against PostgreSQL, bypassing controllers and application services.
/// </summary>
public sealed class HierarchyForeignKeyConstraintTests
  : IClassFixture<PostgreSqlConstraintFixture>
{
  private const string ForeignKeyViolationSqlState = "23503";
  private const string NotNullViolationSqlState = "23502";

  private readonly PostgreSqlConstraintFixture _fixture;

  public HierarchyForeignKeyConstraintTests(
    PostgreSqlConstraintFixture fixture)
  {
    _fixture = fixture;
  }

  [Fact]
  public async Task TerritoryAssignment_WithUnknownAgency_IsRejected()
  {
    await using var db = _fixture.CreateDbContext();

    var seed = await SeedTerritoryAsync(db);
    var nonexistentAgencyId = Guid.NewGuid();

    var exception = await Assert.ThrowsAsync<PostgresException>(
      async () =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO territory_agency_assignment
            (
              assignment_id,
              company_id,
              territory_id,
              agency_id,
              starts_at,
              ends_at,
              created_by
            )
          VALUES
            (
              {Guid.NewGuid()},
              {seed.CompanyId},
              {seed.TerritoryId},
              {nonexistentAgencyId},
              {DateTimeOffset.UtcNow},
              NULL,
              {"foreign-key-test"}
            )
          """));

    Assert.Equal(
      ForeignKeyViolationSqlState,
      exception.SqlState);

    Assert.Equal(
      "fk_territory_agency_assignment_agency",
      exception.ConstraintName);

    db.ChangeTracker.Clear();

    var assignmentCount =
      await db.TerritoryAgencyAssignments
        .IgnoreQueryFilters()
        .CountAsync(assignment =>
          assignment.TerritoryId == seed.TerritoryId);

    Assert.Equal(0, assignmentCount);
  }

  [Fact]
  public async Task TerritoryAssignment_WithoutAgency_IsRejected()
  {
    await using var db = _fixture.CreateDbContext();

    var seed = await SeedTerritoryAsync(db);

    var exception = await Assert.ThrowsAsync<PostgresException>(
      async () =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO territory_agency_assignment
            (
              assignment_id,
              company_id,
              territory_id,
              starts_at,
              ends_at,
              created_by
            )
          VALUES
            (
              {Guid.NewGuid()},
              {seed.CompanyId},
              {seed.TerritoryId},
              {DateTimeOffset.UtcNow},
              NULL,
              {"not-null-test"}
            )
          """));

    Assert.Equal(
      NotNullViolationSqlState,
      exception.SqlState);

    Assert.Equal("agency_id", exception.ColumnName);

    db.ChangeTracker.Clear();

    var assignmentCount =
      await db.TerritoryAgencyAssignments
        .IgnoreQueryFilters()
        .CountAsync(assignment =>
          assignment.TerritoryId == seed.TerritoryId);

    Assert.Equal(0, assignmentCount);
  }

  [Fact]
  public async Task Shop_WithUnknownTerritory_IsRejected()
  {
    await using var db = _fixture.CreateDbContext();

    var companyId = await SeedCompanyAsync(db);
    var nonexistentTerritoryId = Guid.NewGuid();

    var exception = await Assert.ThrowsAsync<PostgresException>(
      async () => await InsertShopAsync(
        db,
        companyId,
        nonexistentTerritoryId));

    Assert.Equal(
      ForeignKeyViolationSqlState,
      exception.SqlState);

    Assert.Equal(
      "fk_shop_territory",
      exception.ConstraintName);

    db.ChangeTracker.Clear();

    var shopCount = await db.Shops
      .IgnoreQueryFilters()
      .CountAsync(shop =>
        shop.CompanyId == companyId);

    Assert.Equal(0, shopCount);
  }

  [Fact]
  public async Task Shop_WithoutTerritory_IsRejected()
  {
    await using var db = _fixture.CreateDbContext();

    var companyId = await SeedCompanyAsync(db);

    var exception = await Assert.ThrowsAsync<PostgresException>(
      async () =>
        await db.Database.ExecuteSqlInterpolatedAsync($"""
          INSERT INTO shop
            (
              shop_id,
              company_id,
              name,
              address,
              latitude,
              longitude,
              credit_limit,
              status,
              created_at
            )
          VALUES
            (
              {Guid.NewGuid()},
              {companyId},
              {"Shop Without Territory"},
              {"123 Test Road"},
              {6.927079m},
              {79.861244m},
              {10000.00m},
              {"Active"},
              {DateTimeOffset.UtcNow}
            )
          """));

    Assert.Equal(
      NotNullViolationSqlState,
      exception.SqlState);

    Assert.Equal("territory_id", exception.ColumnName);

    db.ChangeTracker.Clear();

    var shopCount = await db.Shops
      .IgnoreQueryFilters()
      .CountAsync(shop =>
        shop.CompanyId == companyId);

    Assert.Equal(0, shopCount);
  }

  private static async Task<TerritorySeed>
    SeedTerritoryAsync(CoreDbContext db)
  {
    var companyId = Guid.NewGuid();
    var provinceId = Guid.NewGuid();
    var territoryId = Guid.NewGuid();
    var suffix = Guid.NewGuid().ToString("N")[..8];
    var now = DateTimeOffset.UtcNow;

    db.AddRange(
      new Company
      {
        CompanyId = companyId,
        TenantCode = $"tenant-{suffix}",
        Name = $"Test Company {suffix}",
        Status = "Active",
        CreatedAt = now
      },
      new Province
      {
        ProvinceId = provinceId,
        CompanyId = companyId,
        Code = $"P-{suffix}",
        Name = $"Test Province {suffix}",
        Status = "Active",
        CreatedAt = now
      },
      new Territory
      {
        TerritoryId = territoryId,
        CompanyId = companyId,
        ProvinceId = provinceId,
        Code = $"T-{suffix}",
        Name = $"Test Territory {suffix}",
        Status = "Active",
        CreatedAt = now
      });

    await db.SaveChangesAsync();

    return new TerritorySeed(companyId, territoryId);
  }

  private static async Task<Guid>
    SeedCompanyAsync(CoreDbContext db)
  {
    var companyId = Guid.NewGuid();
    var suffix = Guid.NewGuid().ToString("N")[..8];

    db.Companies.Add(new Company
    {
      CompanyId = companyId,
      TenantCode = $"tenant-{suffix}",
      Name = $"Test Company {suffix}",
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    });

    await db.SaveChangesAsync();

    return companyId;
  }

  private static async Task InsertShopAsync(
    CoreDbContext db,
    Guid companyId,
    Guid territoryId)
  {
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO shop
        (
          shop_id,
          company_id,
          territory_id,
          name,
          address,
          latitude,
          longitude,
          credit_limit,
          status,
          created_at
        )
      VALUES
        (
          {Guid.NewGuid()},
          {companyId},
          {territoryId},
          {"Orphan Test Shop"},
          {"123 Test Road"},
          {6.927079m},
          {79.861244m},
          {10000.00m},
          {"Active"},
          {DateTimeOffset.UtcNow}
        )
      """);
  }

  private sealed record TerritorySeed(
    Guid CompanyId,
    Guid TerritoryId);
}