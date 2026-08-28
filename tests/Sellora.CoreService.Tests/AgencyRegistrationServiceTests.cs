using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Agencies;
using Sellora.CoreService.Infrastructure.Outbox;
using Sellora.CoreService.Infrastructure.Persistence;
using Xunit;
using Sellora.CoreService.Tests;
using Sellora.CoreService.Application.Outbox;

namespace Sellora.CoreService.Tests.Agencies;

/// <summary>
/// End-to-end verification that (province, name) uniqueness is enforced
/// at the database level and surfaced cleanly through the service. Runs
/// against a real Postgres from the shared fixture — an in-memory EF
/// provider would silently pass a buggy implementation because it does
/// not honour unique indexes.
/// </summary>
public sealed class AgencyRegistrationServiceTests
  : IClassFixture<PostgreSqlConstraintFixture>, IAsyncLifetime
{
  private readonly PostgreSqlConstraintFixture _fixture;

  private readonly Guid _companyId = Guid.NewGuid();
  private readonly Guid _westernProvinceId = Guid.NewGuid();
  private readonly Guid _areaManagerProfileId = Guid.NewGuid();
  private readonly string _managerSub = $"test-sub:area-manager:{Guid.NewGuid():N}";
  private const string DuplicateName = "Colombo Distribution Agency";

  private readonly Guid _operatorProfileId = Guid.NewGuid();
  private readonly string _operatorSub = $"test-sub:agency-operator:{Guid.NewGuid():N}";

  public AgencyRegistrationServiceTests(PostgreSqlConstraintFixture fixture)
  {
    _fixture = fixture;
  }

  public async Task InitializeAsync()
  {

    await using var db = _fixture.CreateDbContext(_companyId);

    db.Companies.Add(new Company
    {
      CompanyId = _companyId,
      TenantCode = $"TEST-CO-{_companyId:N}",
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
      IdentitySub = _managerSub,
      Role = Roles.AreaManager,
      DisplayName = "Test Area Manager",
      Email = $"manager-{_areaManagerProfileId:N}@test.local",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = _operatorProfileId,
      CompanyId = _companyId,
      IdentitySub = _operatorSub,
      Role = Roles.AgencyOperator,
      DisplayName = "Test Agency Operator",
      Email = $"operator-{_operatorProfileId:N}@test.local",
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
    var currentUser = new FakeCurrentUserContext(_managerSub);

    await using var db1 = _fixture.CreateDbContext(_companyId);
    var service1 = new TerritoryRegistrationServiceHost(db1, currentUser);

    // Act 1 — first registration succeeds.
    var first = await service1.Service.RegisterAsync(new RegisterAgencyRequest(
      _westernProvinceId,
      _operatorProfileId,
      DuplicateName,
      Email: "colombo@test.local",
      Phone: null,
      Address: null));

    await using var verifyDb = _fixture.CreateDbContext(_companyId);

    await OutboxEventAssertions.AssertEventAsync(
      verifyDb,
      "AgencyRegistered",
      _companyId,
      first.Agency!.AgencyId,
      ("agencyId", first.Agency.AgencyId),
      ("provinceId", _westernProvinceId),
      ("operatorId", _operatorProfileId));

    var link = await verifyDb.AgencyOperatorAssignments
      .SingleAsync(assignment =>
        assignment.AgencyId == first.Agency!.AgencyId &&
        assignment.OperatorId == _operatorProfileId &&
        assignment.EndsAt == null);

    link.CompanyId.Should().Be(_companyId);

    // Act 2 — fresh context so we're not relying on in-memory dedupe;
    // the constraint MUST fire at the database.
    await using var db2 = _fixture.CreateDbContext(_companyId);
    var service2 = new TerritoryRegistrationServiceHost(db2, currentUser);

    var second = await service2.Service.RegisterAsync(new RegisterAgencyRequest(
      _westernProvinceId,
      _operatorProfileId,
      DuplicateName,
      Email: "colombo@test.local",
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
    ex.WithInnerException<PostgresException>()
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
      var correlationAccessor = new FakeCorrelationIdAccessor();
      var outboxWriter = new EntityFrameworkOutboxWriter(db, correlationAccessor);
      var eventFactory = new HierarchyEventFactory(correlationAccessor);

      Service = new AgencyRegistrationService(
        db,
        currentUser,
        NullLogger<AgencyRegistrationService>.Instance,
        outboxWriter,
        eventFactory);
    }
  }

  private sealed class FakeCurrentUserContext : ICurrentUserContext
  {
    public FakeCurrentUserContext(string subject) { Subject = subject; }
    public string? Subject { get; }
    public string? Role => Roles.AreaManager;
  }

  private sealed class FakeCorrelationIdAccessor : ICorrelationIdAccessor
  {
    public string GetCorrelationId() => "test-correlation-id";
  }
}