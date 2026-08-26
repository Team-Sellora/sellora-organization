using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Agencies;
using Sellora.CoreService.Infrastructure.Persistence;
using Xunit;

namespace Sellora.CoreService.Tests.Agencies;

/// <summary>
/// End-to-end verification that (province, name) uniqueness is enforced
/// at the database level and surfaced cleanly through the service. Runs
/// against a real Postgres from the shared fixture — an in-memory EF
/// provider would silently pass a buggy implementation because it does
/// not honour unique indexes.
/// </summary>
public sealed class AgencyRegistrationServiceTests
  : IClassFixture<PostgresFixture>, IAsyncLifetime
{
  private readonly PostgresFixture _fixture;

  private readonly Guid _companyId = Guid.NewGuid();
  private readonly Guid _westernProvinceId = Guid.NewGuid();
  private readonly Guid _areaManagerProfileId = Guid.NewGuid();
  private const string ManagerSub = "test-sub:area-manager";
  private const string DuplicateName = "Colombo Distribution Agency";

  public AgencyRegistrationServiceTests(PostgresFixture fixture)
  {
    _fixture = fixture;
  }

  public async Task InitializeAsync()
  {
    await _fixture.ResetAsync();

    await using var db = _fixture.CreateDbContext(_companyId);

    db.Companies.Add(new Company
    {
      CompanyId = _companyId,
      TenantCode = "TEST-CO",
      Name = "Test Company",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.Provinces.Add(new Province
    {
      ProvinceId = _westernProvinceId,
      CompanyId = _companyId,
      Code = "WP",
      Name = "Western Province",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = _areaManagerProfileId,
      CompanyId = _companyId,
      IdentitySub = ManagerSub,
      Role = Roles.AreaManager,
      DisplayName = "Test Area Manager",
      Email = "am@test.local",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.ProvinceManagerAssignments.Add(new ProvinceManagerAssignment
    {
      AssignmentId = Guid.NewGuid(),
      CompanyId = _companyId,
      ProvinceId = _westernProvinceId,
      AreaManagerId = _areaManagerProfileId,
      StartsAt = DateTimeOffset.UtcNow,
      CreatedBy = "test"
    });

    await db.SaveChangesAsync();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  /// <summary>
  /// The service-level contract: a second registration with the same
  /// (province, name) is rejected with DuplicateAgencyName, and the
  /// message names the existing agency so the caller knows the conflict.
  /// </summary>
  [Fact]
  public async Task RegisterAsync_DuplicateNameInSameProvince_RejectsSecondAndNamesExisting()
  {
    // Arrange
    var currentUser = new FakeCurrentUserContext(ManagerSub);

    await using var db1 = _fixture.CreateDbContext(_companyId);
    var service1 = new TerritoryRegistrationServiceHost(db1, currentUser);

    // Act 1 — first registration succeeds.
    var first = await service1.Service.RegisterAsync(new RegisterAgencyRequest(
      _westernProvinceId,
      DuplicateName,
      Email: "colombo@test.local",
      Phone: null,
      Address: null));

    // Act 2 — fresh context so we're not relying on in-memory dedupe;
    // the constraint MUST fire at the database.
    await using var db2 = _fixture.CreateDbContext(_companyId);
    var service2 = new TerritoryRegistrationServiceHost(db2, currentUser);

    var second = await service2.Service.RegisterAsync(new RegisterAgencyRequest(
      _westernProvinceId,
      DuplicateName,
      Email: "colombo-2@test.local",
      Phone: null,
      Address: null));

    // Assert — first succeeded, second rejected with the right outcome
    // and a message that names the existing row (that is the CSP-69
    // deliverable over what CSP-67 already returned).
    first.Outcome.Should().Be(RegisterAgencyOutcome.Success);
    first.Agency!.Name.Should().Be(DuplicateName);

    second.Outcome.Should().Be(RegisterAgencyOutcome.DuplicateAgencyName);
    second.Agency.Should().BeNull();
    second.Message.Should().Contain(
      first.Agency.AgencyId.ToString(),
      "the error must name the existing agency (CSP-69 acceptance criterion)");

    // And the DB still holds exactly one row with that name.
    await using var verifyDb = _fixture.CreateDbContext(_companyId);
    var count = await verifyDb.Agencies
      .CountAsync(a =>
        a.ProvinceId == _westernProvinceId &&
        a.Name == DuplicateName);
    count.Should().Be(1);
  }

  /// <summary>
  /// The invariant lives at the database, not just in the service. If the
  /// service catch were ever removed or moved, the DB constraint alone
  /// must still block the duplicate. This test bypasses the service and
  /// writes directly, expecting a Postgres unique-violation on the
  /// specific constraint name.
  /// </summary>
  [Fact]
  public async Task Database_RejectsDuplicateAgencyName_WhenServiceIsBypassed()
  {
    await using var db = _fixture.CreateDbContext(_companyId);

    db.Agencies.Add(new Agency
    {
      AgencyId = Guid.NewGuid(),
      CompanyId = _companyId,
      ProvinceId = _westernProvinceId,
      Name = DuplicateName,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync();

    // Second row with the same (province_id, name) — the DB must reject.
    await using var db2 = _fixture.CreateDbContext(_companyId);
    db2.Agencies.Add(new Agency
    {
      AgencyId = Guid.NewGuid(),
      CompanyId = _companyId,
      ProvinceId = _westernProvinceId,
      Name = DuplicateName,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    var act = async () => await db2.SaveChangesAsync();

    var ex = await act.Should().ThrowAsync<DbUpdateException>();
    ex.WithInnerException<DbUpdateException, PostgresException>()
      .Which.ConstraintName.Should().Be(
        "uq_agency_province_name",
        "the composite index on (province_id, name) is the guarantee");
  }

  /// <summary>
  /// Small holder so the service's dependencies are constructed once per
  /// context — keeps the [Fact] bodies focused on scenario, not wiring.
  /// </summary>
  private sealed class TerritoryRegistrationServiceHost
  {
    public AgencyRegistrationService Service { get; }

    public TerritoryRegistrationServiceHost(
      CoreDbContext db,
      ICurrentUserContext currentUser)
    {
      Service = new AgencyRegistrationService(
        db,
        currentUser,
        NullLogger<AgencyRegistrationService>.Instance);
    }
  }

  private sealed class FakeCurrentUserContext : ICurrentUserContext
  {
    public FakeCurrentUserContext(string subject) { Subject = subject; }
    public string? Subject { get; }
    public string? Role => Roles.AreaManager;
  }
}