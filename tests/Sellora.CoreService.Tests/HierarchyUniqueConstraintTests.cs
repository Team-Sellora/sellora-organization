using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Verifies hierarchy uniqueness rules directly against PostgreSQL.
/// These tests bypass controllers and application services.
/// </summary>
public sealed class HierarchyUniqueConstraintTests
  : IClassFixture<PostgreSqlConstraintFixture>
{
  private const string UniqueViolationSqlState = "23505";

  private readonly PostgreSqlConstraintFixture _fixture;

  public HierarchyUniqueConstraintTests(
    PostgreSqlConstraintFixture fixture)
  {
    _fixture = fixture;
  }

  [Fact]
  public async Task SecondActiveRepForTerritory_IsRejectedByDatabase()
  {
    await using var db = _fixture.CreateDbContext();

    var seed = await SeedCompanyAndProvinceAsync(db);
    var territoryId = Guid.NewGuid();
    var firstRepId = Guid.NewGuid();
    var secondRepId = Guid.NewGuid();
    var firstAssignmentId = Guid.NewGuid();
    var secondAssignmentId = Guid.NewGuid();
    var startsAt = DateTimeOffset.UtcNow;

    db.AddRange(
      CreateTerritory(
        territoryId,
        seed.CompanyId,
        seed.ProvinceId,
        $"T-{Guid.NewGuid():N}",
        "Northern Territory"),
      CreateStaff(
        firstRepId,
        seed.CompanyId,
        $"rep-{Guid.NewGuid():N}",
        "SalesRep",
        "First Sales Rep"),
      CreateStaff(
        secondRepId,
        seed.CompanyId,
        $"rep-{Guid.NewGuid():N}",
        "SalesRep",
        "Second Sales Rep"));

    await db.SaveChangesAsync();

    await InsertSalesRepAssignmentAsync(
      db,
      firstAssignmentId,
      seed.CompanyId,
      territoryId,
      firstRepId,
      startsAt);

    var exception = await Assert.ThrowsAsync<PostgresException>(
      async () => await InsertSalesRepAssignmentAsync(
        db,
        secondAssignmentId,
        seed.CompanyId,
        territoryId,
        secondRepId,
        startsAt));

    Assert.Equal(UniqueViolationSqlState, exception.SqlState);
    Assert.Equal(
      "uq_sales_rep_assignment_active_territory",
      exception.ConstraintName);

    db.ChangeTracker.Clear();

    var assignmentsAfterRejectedWrite =
      await db.SalesRepTerritoryAssignments
        .IgnoreQueryFilters()
        .Where(assignment =>
          assignment.TerritoryId == territoryId)
        .ToListAsync();

    var existingAssignment =
      Assert.Single(assignmentsAfterRejectedWrite);

    Assert.Equal(firstRepId, existingAssignment.SalesRepId);
    Assert.Null(existingAssignment.EndsAt);

    // Once the old assignment has ended, its historical row must not
    // conflict with the new active assignment.
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      UPDATE sales_rep_territory_assignment
      SET ends_at = {startsAt.AddMinutes(1)}
      WHERE assignment_id = {firstAssignmentId}
      """);

    await InsertSalesRepAssignmentAsync(
      db,
      secondAssignmentId,
      seed.CompanyId,
      territoryId,
      secondRepId,
      startsAt.AddMinutes(2));

    db.ChangeTracker.Clear();

    var finalAssignmentCount =
      await db.SalesRepTerritoryAssignments
        .IgnoreQueryFilters()
        .CountAsync(assignment =>
          assignment.TerritoryId == territoryId);

    Assert.Equal(2, finalAssignmentCount);
  }

  [Fact]
  public async Task SecondActiveAreaManagerForProvince_IsRejectedByDatabase()
  {
    await using var db = _fixture.CreateDbContext();

    var seed = await SeedCompanyAndProvinceAsync(db);
    var firstManagerId = Guid.NewGuid();
    var secondManagerId = Guid.NewGuid();
    var firstAssignmentId = Guid.NewGuid();
    var secondAssignmentId = Guid.NewGuid();
    var startsAt = DateTimeOffset.UtcNow;

    db.AddRange(
      CreateStaff(
        firstManagerId,
        seed.CompanyId,
        $"manager-{Guid.NewGuid():N}",
        "AreaManager",
        "First Area Manager"),
      CreateStaff(
        secondManagerId,
        seed.CompanyId,
        $"manager-{Guid.NewGuid():N}",
        "AreaManager",
        "Second Area Manager"));

    await db.SaveChangesAsync();

    await InsertProvinceManagerAssignmentAsync(
      db,
      firstAssignmentId,
      seed.CompanyId,
      seed.ProvinceId,
      firstManagerId,
      startsAt);

    var exception = await Assert.ThrowsAsync<PostgresException>(
      async () => await InsertProvinceManagerAssignmentAsync(
        db,
        secondAssignmentId,
        seed.CompanyId,
        seed.ProvinceId,
        secondManagerId,
        startsAt));

    Assert.Equal(UniqueViolationSqlState, exception.SqlState);
    Assert.Equal(
      "uq_province_manager_assignment_active_province",
      exception.ConstraintName);

    db.ChangeTracker.Clear();

    var assignmentsAfterRejectedWrite =
      await db.ProvinceManagerAssignments
        .IgnoreQueryFilters()
        .Where(assignment =>
          assignment.ProvinceId == seed.ProvinceId)
        .ToListAsync();

    var existingAssignment =
      Assert.Single(assignmentsAfterRejectedWrite);

    Assert.Equal(
      firstManagerId,
      existingAssignment.AreaManagerId);

    Assert.Null(existingAssignment.EndsAt);

    await db.Database.ExecuteSqlInterpolatedAsync($"""
      UPDATE province_manager_assignment
      SET ends_at = {startsAt.AddMinutes(1)}
      WHERE assignment_id = {firstAssignmentId}
      """);

    await InsertProvinceManagerAssignmentAsync(
      db,
      secondAssignmentId,
      seed.CompanyId,
      seed.ProvinceId,
      secondManagerId,
      startsAt.AddMinutes(2));

    db.ChangeTracker.Clear();

    var finalAssignmentCount =
      await db.ProvinceManagerAssignments
        .IgnoreQueryFilters()
        .CountAsync(assignment =>
          assignment.ProvinceId == seed.ProvinceId);

    Assert.Equal(2, finalAssignmentCount);
  }

  [Fact]
  public async Task DuplicateAgencyNameWithinProvince_IsRejectedByDatabase()
  {
    await using var db = _fixture.CreateDbContext();

    var seed = await SeedCompanyAndProvinceAsync(db);
    var agencyName = $"Central Agency {Guid.NewGuid():N}";

    await InsertAgencyAsync(
      db,
      Guid.NewGuid(),
      seed.CompanyId,
      seed.ProvinceId,
      agencyName);

    var exception = await Assert.ThrowsAsync<PostgresException>(
      async () => await InsertAgencyAsync(
        db,
        Guid.NewGuid(),
        seed.CompanyId,
        seed.ProvinceId,
        agencyName));

    Assert.Equal(UniqueViolationSqlState, exception.SqlState);
    Assert.Equal(
      "uq_agency_province_name",
      exception.ConstraintName);

    db.ChangeTracker.Clear();

    var agencyCount = await db.Agencies
      .IgnoreQueryFilters()
      .CountAsync(agency =>
        agency.ProvinceId == seed.ProvinceId &&
        agency.Name == agencyName);

    Assert.Equal(1, agencyCount);
  }

  [Fact]
  public async Task DuplicateTerritoryCodeWithinCompany_IsRejectedByDatabase()
  {
    await using var db = _fixture.CreateDbContext();

    var seed = await SeedCompanyAndProvinceAsync(db);
    var secondProvinceId = Guid.NewGuid();
    var territoryCode = $"T-{Guid.NewGuid():N}";

    db.Provinces.Add(new Province
    {
      ProvinceId = secondProvinceId,
      CompanyId = seed.CompanyId,
      Code = $"P-{Guid.NewGuid().ToString("N")[..8]}",
      Name = $"Second Province {Guid.NewGuid():N}",
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    });

    await db.SaveChangesAsync();

    await InsertTerritoryAsync(
      db,
      Guid.NewGuid(),
      seed.CompanyId,
      seed.ProvinceId,
      territoryCode,
      "First Territory");

    var exception = await Assert.ThrowsAsync<PostgresException>(
      async () => await InsertTerritoryAsync(
        db,
        Guid.NewGuid(),
        seed.CompanyId,
        secondProvinceId,
        territoryCode,
        "Second Territory"));

    Assert.Equal(UniqueViolationSqlState, exception.SqlState);
    Assert.Equal(
      "uq_territory_company_code",
      exception.ConstraintName);

    db.ChangeTracker.Clear();

    var territoryCount = await db.Territories
      .IgnoreQueryFilters()
      .CountAsync(territory =>
        territory.CompanyId == seed.CompanyId &&
        territory.Code == territoryCode);

    Assert.Equal(1, territoryCount);
  }

  private static async Task<HierarchySeed>
    SeedCompanyAndProvinceAsync(CoreDbContext db)
  {
    var companyId = Guid.NewGuid();
    var provinceId = Guid.NewGuid();
    var suffix = Guid.NewGuid().ToString("N")[..8];

    db.Companies.Add(new Company
    {
      CompanyId = companyId,
      TenantCode = $"tenant-{suffix}",
      Name = $"Test Company {suffix}",
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.Provinces.Add(new Province
    {
      ProvinceId = provinceId,
      CompanyId = companyId,
      Code = $"P-{suffix}",
      Name = $"Test Province {suffix}",
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    });

    await db.SaveChangesAsync();

    return new HierarchySeed(companyId, provinceId);
  }

  private static StaffProfile CreateStaff(
    Guid staffProfileId,
    Guid companyId,
    string identitySub,
    string role,
    string displayName)
  {
    return new StaffProfile
    {
      StaffProfileId = staffProfileId,
      CompanyId = companyId,
      IdentitySub = identitySub,
      Role = role,
      DisplayName = displayName,
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    };
  }

  private static Territory CreateTerritory(
    Guid territoryId,
    Guid companyId,
    Guid provinceId,
    string code,
    string name)
  {
    return new Territory
    {
      TerritoryId = territoryId,
      CompanyId = companyId,
      ProvinceId = provinceId,
      Code = code,
      Name = name,
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    };
  }

  private static async Task InsertSalesRepAssignmentAsync(
    CoreDbContext db,
    Guid assignmentId,
    Guid companyId,
    Guid territoryId,
    Guid salesRepId,
    DateTimeOffset startsAt)
  {
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO sales_rep_territory_assignment
        (
          assignment_id,
          company_id,
          territory_id,
          sales_rep_id,
          starts_at,
          ends_at,
          created_by
        )
      VALUES
        (
          {assignmentId},
          {companyId},
          {territoryId},
          {salesRepId},
          {startsAt},
          NULL,
          {"constraint-test"}
        )
      """);
  }

  private static async Task InsertProvinceManagerAssignmentAsync(
    CoreDbContext db,
    Guid assignmentId,
    Guid companyId,
    Guid provinceId,
    Guid managerId,
    DateTimeOffset startsAt)
  {
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO province_manager_assignment
        (
          assignment_id,
          company_id,
          province_id,
          area_manager_id,
          reports_to_admin_id,
          starts_at,
          ends_at,
          created_by
        )
      VALUES
        (
          {assignmentId},
          {companyId},
          {provinceId},
          {managerId},
          NULL,
          {startsAt},
          NULL,
          {"constraint-test"}
        )
      """);
  }

  private static async Task InsertAgencyAsync(
    CoreDbContext db,
    Guid agencyId,
    Guid companyId,
    Guid provinceId,
    string name)
  {
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO agency
        (
          agency_id,
          company_id,
          province_id,
          name,
          status,
          created_at
        )
      VALUES
        (
          {agencyId},
          {companyId},
          {provinceId},
          {name},
          {"Active"},
          {DateTimeOffset.UtcNow}
        )
      """);
  }

  private static async Task InsertTerritoryAsync(
    CoreDbContext db,
    Guid territoryId,
    Guid companyId,
    Guid provinceId,
    string code,
    string name)
  {
    await db.Database.ExecuteSqlInterpolatedAsync($"""
      INSERT INTO territory
        (
          territory_id,
          company_id,
          province_id,
          code,
          name,
          status,
          created_at
        )
      VALUES
        (
          {territoryId},
          {companyId},
          {provinceId},
          {code},
          {name},
          {"Active"},
          {DateTimeOffset.UtcNow}
        )
      """);
  }

  private sealed record HierarchySeed(
    Guid CompanyId,
    Guid ProvinceId);
}