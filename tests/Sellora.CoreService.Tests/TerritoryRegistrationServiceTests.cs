using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using Sellora.CoreService.Infrastructure.Territories;
using Xunit;
using Sellora.CoreService.Tests;
namespace Sellora.CoreService.Tests.Territories;

/// <summary>
/// Uses the shared Postgres fixture from the test project. Adapt the
/// constructor argument if your fixture is named differently — the point
/// is that the tests run against a real Postgres so unique-index behaviour
/// is exercised end-to-end. An in-memory EF provider will NOT enforce the
/// unique index and would silently pass a buggy implementation.
/// </summary>
public sealed class TerritoryRegistrationServiceTests
  : IClassFixture<PostgreSqlConstraintFixture>, IAsyncLifetime
{
  private readonly PostgreSqlConstraintFixture _fixture;

  // Company-scoped test data seeded per-test in InitializeAsync.
  private readonly Guid _companyId = Guid.NewGuid();
  private readonly Guid _westernProvinceId = Guid.NewGuid();
  private readonly Guid _centralProvinceId = Guid.NewGuid();
  private readonly Guid _areaManagerProfileId = Guid.NewGuid();
  private readonly string _managerSub = $"test-sub:area-manager:{Guid.NewGuid():N}";
  private const string DuplicateCode = "WP-T-01";

  public TerritoryRegistrationServiceTests(PostgreSqlConstraintFixture fixture)
  {
    _fixture = fixture;
  }

  public async Task InitializeAsync()
  {

    // Seed a company, two provinces, one Area Manager, and assign that
    // manager to BOTH provinces so a single caller can attempt to create
    // territories in each. This is the specific setup CSP-68's test asks
    // for: two provinces in the same company, same actor.
    await using var db = _fixture.CreateDbContext(_companyId);

    db.Companies.Add(new Company
    {
      CompanyId = _companyId,
      TenantCode = $"TEST-CO-{_companyId:N}",
      Name = "Test Company",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.Provinces.AddRange(
      new Province
      {
        ProvinceId = _westernProvinceId,
        CompanyId = _companyId,
        Code = "WP",
        Name = "Western Province",
        Status = HierarchyStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow
      },
      new Province
      {
        ProvinceId = _centralProvinceId,
        CompanyId = _companyId,
        Code = "CP",
        Name = "Central Province",
        Status = HierarchyStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow
      });

    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = _areaManagerProfileId,
      CompanyId = _companyId,
      IdentitySub = _managerSub,
      Role = Roles.AreaManager,
      DisplayName = "Test Area Manager",
      Email = "am@test.local",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.ProvinceManagerAssignments.AddRange(
      new ProvinceManagerAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = _companyId,
        ProvinceId = _westernProvinceId,
        AreaManagerId = _areaManagerProfileId,
        StartsAt = DateTimeOffset.UtcNow,
        CreatedBy = "test"
      },
      new ProvinceManagerAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = _companyId,
        ProvinceId = _centralProvinceId,
        AreaManagerId = _areaManagerProfileId,
        StartsAt = DateTimeOffset.UtcNow,
        CreatedBy = "test"
      });

    await db.SaveChangesAsync();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  [Fact]
  public async Task RegisterAsync_SameCodeInDifferentProvincesOfSameCompany_RejectsSecond()
  {
    // Arrange — one caller acting as the manager of both provinces.
    var currentUser = new FakeCurrentUserContext(_managerSub);

    await using var db = _fixture.CreateDbContext(_companyId);
    var service = new TerritoryRegistrationService(
      db,
      currentUser,
      NullLogger<TerritoryRegistrationService>.Instance);

    // Act 1 — create territory WP-T-01 in the Western province.
    var first = await service.RegisterAsync(new RegisterTerritoryRequest(
      _westernProvinceId,
      DuplicateCode,
      "Colombo Central",
      GeographicDescription: null));

    // Act 2 — attempt the SAME code in the Central province of the SAME
    // company. This is the scenario CSP-68 asks for: uniqueness is at the
    // company level, so a different province is not an escape hatch.
    // A fresh DbContext ensures we're not just seeing an in-memory dedupe.
    await using var db2 = _fixture.CreateDbContext(_companyId);
    var service2 = new TerritoryRegistrationService(
      db2,
      currentUser,
      NullLogger<TerritoryRegistrationService>.Instance);

    var second = await service2.RegisterAsync(new RegisterTerritoryRequest(
      _centralProvinceId,
      DuplicateCode,
      "Kandy City",
      GeographicDescription: null));

    // Assert
    first.Outcome.Should().Be(RegisterTerritoryOutcome.Success);
    first.Territory!.Code.Should().Be(DuplicateCode);
    first.Territory.ProvinceId.Should().Be(_westernProvinceId);

    second.Outcome.Should().Be(RegisterTerritoryOutcome.DuplicateTerritoryCode);
    second.Territory.Should().BeNull();
    second.Message.Should().Contain(DuplicateCode,
      "the error message must name the conflicting code (AC3 of US-E1-3)");

    // And the original territory must be unchanged — no partial write.
    await using var verifyDb = _fixture.CreateDbContext(_companyId);
    var territoriesWithCode = await verifyDb.Territories
      .Where(t => t.Code == DuplicateCode)
      .ToListAsync();

    territoriesWithCode.Should().ContainSingle()
      .Which.ProvinceId.Should().Be(_westernProvinceId);
  }

  /// <summary>
  /// Minimal ICurrentUserContext stand-in — returns a fixed sub, no HTTP
  /// context required. Test-only; production uses HttpCurrentUserContext.
  /// </summary>
  private sealed class FakeCurrentUserContext : ICurrentUserContext
  {
    public FakeCurrentUserContext(string subject) { Subject = subject; }
    public string? Subject { get; }
    public string? Role => Roles.AreaManager;
  }
}