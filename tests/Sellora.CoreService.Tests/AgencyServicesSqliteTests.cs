using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Agencies;
using Sellora.CoreService.Infrastructure.AreaManagers;
using Sellora.CoreService.Infrastructure.Hierarchy;
using Sellora.CoreService.Infrastructure.Outbox;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Agency registration, listing, and deactivation against the shared
/// two-company test hierarchy. Uniqueness constraints are covered by the
/// PostgreSQL tests; these cover the scoping and validation paths.
/// </summary>
public sealed class AgencyServicesSqliteTests : IDisposable
{
  private static readonly Guid CompanyId = HierarchyEndpointTestData.CompanyId;

  private readonly SqliteCoreDatabase _database = new();

  public AgencyServicesSqliteTests()
  {
    using var db = _database.CreateDbContext();
    HierarchyEndpointTestData.Seed(db);
  }

  public void Dispose() => _database.Dispose();

  // ------------------------------------------------------------ Registration

  [Fact]
  public async Task Register_ByProvinceManager_CreatesAgencyOperatorLinkAndEvent()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var operatorId = await StaffIdAsync(db, HierarchyEndpointTestData.AgencyOperatorSubject);

    var result = await RegistrationService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(new RegisterAgencyRequest(
        HierarchyEndpointTestData.NorthProvinceId,
        operatorId,
        "  Second North Agency  ",
        Email: " second@agency.test ",
        Phone: " 0771234567 ",
        Address: " 2 North Road "));

    result.Outcome.Should().Be(RegisterAgencyOutcome.Success);
    result.IsSuccess.Should().BeTrue();
    result.Agency!.Name.Should().Be("Second North Agency");
    result.Agency.Email.Should().Be("second@agency.test");
    result.Agency.Phone.Should().Be("0771234567");
    result.Agency.Address.Should().Be("2 North Road");
    result.Agency.CompanyId.Should().Be(CompanyId);

    await using var verify = _database.CreateDbContext(CompanyId);

    (await verify.AgencyOperatorAssignments.AnyAsync(assignment =>
        assignment.AgencyId == result.Agency.AgencyId &&
        assignment.OperatorId == operatorId &&
        assignment.EndsAt == null))
      .Should().BeTrue();

    await OutboxEventAssertions.AssertEventAsync(
      verify,
      "AgencyRegistered",
      CompanyId,
      result.Agency.AgencyId,
      ("agencyId", result.Agency.AgencyId),
      ("operatorId", operatorId));
  }

  [Theory]
  [InlineData(" ", false, "Agency name is required.")]
  [InlineData("Valid Name", true, "provinceId is required.")]
  public async Task Register_InvalidBody_IsRejectedBeforeAnyLookup(
    string name,
    bool emptyProvince,
    string expectedMessage)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await RegistrationService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(new RegisterAgencyRequest(
        emptyProvince ? Guid.Empty : HierarchyEndpointTestData.NorthProvinceId,
        Guid.NewGuid(),
        name,
        null,
        null,
        null));

    result.Outcome.Should().Be(RegisterAgencyOutcome.InvalidRequest);
    result.Message.Should().Be(expectedMessage);
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData(HierarchyEndpointTestData.AgencyOperatorSubject)]
  [InlineData("unknown-subject")]
  public async Task Register_CallerWhoIsNotAnActiveAreaManager_IsRejected(string? subject)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await RegistrationService(db, subject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.NorthProvinceId, Guid.NewGuid()));

    result.Outcome.Should().Be(RegisterAgencyOutcome.CallerNotAnActiveAreaManager);
  }

  [Fact]
  public async Task Register_ProvinceOfAnotherCompany_LooksNotFound()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await RegistrationService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.OtherProvinceId, Guid.NewGuid()));

    result.Outcome.Should().Be(RegisterAgencyOutcome.ProvinceNotFound);
    result.Message.Should().Contain(HierarchyEndpointTestData.OtherProvinceId.ToString());
  }

  [Fact]
  public async Task Register_WithoutOperator_IsInvalid()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await RegistrationService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.NorthProvinceId, Guid.Empty));

    result.Outcome.Should().Be(RegisterAgencyOutcome.InvalidRequest);
    result.Message.Should().Be("operatorId is required.");
  }

  [Fact]
  public async Task Register_UnknownOperator_IsNotFound()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var operatorId = Guid.NewGuid();

    var result = await RegistrationService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.NorthProvinceId, operatorId));

    result.Outcome.Should().Be(RegisterAgencyOutcome.OperatorNotFound);
    result.Message.Should().Contain(operatorId.ToString());
  }

  [Fact]
  public async Task Register_OperatorWhoIsASalesRep_IsRejected()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var salesRepId = await StaffIdAsync(db, HierarchyEndpointTestData.SalesRepSubject);

    var result = await RegistrationService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.NorthProvinceId, salesRepId));

    result.Outcome.Should().Be(RegisterAgencyOutcome.OperatorNotAnActiveAgencyOperator);
  }

  [Fact]
  public async Task Register_InProvinceManagedBySomeoneElse_IsRejected()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var operatorId = await StaffIdAsync(db, HierarchyEndpointTestData.AgencyOperatorSubject);

    var result = await RegistrationService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .RegisterAsync(NewRequest(HierarchyEndpointTestData.SouthProvinceId, operatorId));

    result.Outcome.Should().Be(RegisterAgencyOutcome.ProvinceNotManagedByCaller);
    result.Message.Should().Contain(HierarchyEndpointTestData.SouthProvinceId.ToString());
  }

  // ----------------------------------------------------------------- Listing

  [Theory]
  [InlineData(null)]
  [InlineData(HierarchyEndpointTestData.AgencyOperatorSubject)]
  public async Task List_CallerWhoIsNotAnActiveAreaManager_SeesNothing(string? subject)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var page = await ReadService(db, subject)
      .ListAsync(new AgencyListQuery(HierarchyStatus.Active, null, 1, 25));

    page.Items.Should().BeEmpty();
    page.TotalCount.Should().Be(0);
  }

  [Fact]
  public async Task List_AreaManagerWithoutProvinces_SeesNothing()
  {
    const string subject = "area-manager-without-provinces";

    await using (var setup = _database.CreateDbContext(CompanyId))
    {
      setup.StaffProfiles.Add(new StaffProfile
      {
        StaffProfileId = Guid.NewGuid(),
        CompanyId = CompanyId,
        IdentitySub = subject,
        Role = Roles.AreaManager,
        DisplayName = "Unassigned Manager",
        Status = HierarchyStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow
      });
      await setup.SaveChangesAsync();
    }

    await using var db = _database.CreateDbContext(CompanyId);

    var page = await ReadService(db, subject)
      .ListAsync(new AgencyListQuery(HierarchyStatus.Active, null, 1, 25));

    page.Items.Should().BeEmpty();
  }

  [Fact]
  public async Task List_ReturnsOnlyAgenciesInManagedProvinces()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var page = await ReadService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .ListAsync(new AgencyListQuery(HierarchyStatus.Active, null, 1, 25));

    page.TotalCount.Should().Be(1);
    var agency = page.Items.Should().ContainSingle().Subject;
    agency.AgencyId.Should().Be(HierarchyEndpointTestData.NorthAgencyId);
    agency.Email.Should().Be("north@agency.test");
  }

  [Fact]
  public async Task List_ProvinceFilterOutsideScope_CollapsesToEmpty()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var page = await ReadService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .ListAsync(new AgencyListQuery(
        HierarchyStatus.Active,
        HierarchyEndpointTestData.SouthProvinceId,
        1,
        25));

    page.Items.Should().BeEmpty();
    page.TotalCount.Should().Be(0);
  }

  [Fact]
  public async Task List_PageBeyondTheEnd_KeepsTotalCount()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var page = await ReadService(db, HierarchyEndpointTestData.AreaManagerSubject)
      .ListAsync(new AgencyListQuery(
        HierarchyStatus.Active,
        HierarchyEndpointTestData.NorthProvinceId,
        2,
        25));

    page.Items.Should().BeEmpty();
    page.TotalCount.Should().Be(1);
    page.Page.Should().Be(2);
  }

  // ------------------------------------------------------------ Deactivation

  [Fact]
  public async Task Deactivate_UnknownOrOtherCompanyAgency_ReturnsFalse()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var service = DeactivationService(db);

    (await service.DeactivateAgencyAsync(Guid.NewGuid())).Should().BeFalse();

    await using var otherCompanyDb = _database.CreateDbContext(HierarchyEndpointTestData.OtherCompanyId);
    (await DeactivationService(otherCompanyDb)
        .DeactivateAgencyAsync(HierarchyEndpointTestData.NorthAgencyId))
      .Should().BeFalse("the tenant filter hides another company's agency");
  }

  [Fact]
  public async Task Deactivate_MarksAgencyInactiveOnce_AndIsIdempotent()
  {
    await using (var db = _database.CreateDbContext(CompanyId))
    {
      (await DeactivationService(db)
          .DeactivateAgencyAsync(HierarchyEndpointTestData.SouthAgencyId))
        .Should().BeTrue();
    }

    await using (var db = _database.CreateDbContext(CompanyId))
    {
      (await DeactivationService(db)
          .DeactivateAgencyAsync(HierarchyEndpointTestData.SouthAgencyId))
        .Should().BeTrue("repeating a deactivation is not an error");
    }

    await using var verify = _database.CreateDbContext(CompanyId);

    var agency = await verify.Agencies.SingleAsync(candidate =>
      candidate.AgencyId == HierarchyEndpointTestData.SouthAgencyId);
    agency.Status.Should().Be(HierarchyStatus.Inactive);

    (await verify.OutboxMessages.CountAsync(message =>
        message.AggregateId == HierarchyEndpointTestData.SouthAgencyId))
      .Should().Be(1, "the second call must not emit another event");
  }

  // ----------------------------------------------------------- Area managers

  [Fact]
  public async Task AreaManagers_ListsActiveManagersOfTheCallersCompanyByName()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var managers = await new AreaManagerReadService(db).ListActiveAsync();

    managers.Select(manager => manager.DisplayName)
      .Should().Equal("area-manager-north", "area-manager-south");
    managers.Should().OnlyContain(manager => manager.Status == HierarchyStatus.Active);
  }

  // ----------------------------------------------------------------- Helpers

  private static RegisterAgencyRequest NewRequest(Guid provinceId, Guid operatorId) =>
    new(provinceId, operatorId, "New Agency", null, null, null);

  private static Task<Guid> StaffIdAsync(CoreDbContext db, string subject) =>
    db.StaffProfiles
      .Where(profile => profile.IdentitySub == subject)
      .Select(profile => profile.StaffProfileId)
      .SingleAsync();

  private static AgencyRegistrationService RegistrationService(CoreDbContext db, string? subject)
  {
    var correlation = new FixedCorrelationIdAccessor();

    return new AgencyRegistrationService(
      db,
      new FixedCurrentUserContext(subject, Roles.AreaManager),
      NullLogger<AgencyRegistrationService>.Instance,
      new EntityFrameworkOutboxWriter(db, correlation),
      new HierarchyEventFactory(correlation));
  }

  private static AgencyReadService ReadService(CoreDbContext db, string? subject) =>
    new(
      db,
      new FixedCurrentUserContext(subject, Roles.AreaManager),
      NullLogger<AgencyReadService>.Instance);

  private static HierarchyDeactivationService DeactivationService(CoreDbContext db)
  {
    var correlation = new FixedCorrelationIdAccessor();

    return new HierarchyDeactivationService(
      db,
      new EntityFrameworkOutboxWriter(db, correlation),
      new HierarchyEventFactory(correlation));
  }
}
