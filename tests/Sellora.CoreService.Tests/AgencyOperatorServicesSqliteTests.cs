using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Sellora.CoreService.Application.IdentityProvisioning;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Application.Staff;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Outbox;
using Sellora.CoreService.Infrastructure.Persistence;
using Sellora.CoreService.Infrastructure.SalesRepAssignments;
using Sellora.CoreService.Infrastructure.Shops;
using Sellora.CoreService.Infrastructure.Staff;

namespace Sellora.CoreService.Tests;

/// <summary>
/// The rejection paths of the Agency Operator workflows (Sales Rep
/// assignment, shop registration and update) and of staff provisioning,
/// against the shared two-company test hierarchy. Happy paths are covered
/// end to end by the endpoint tests.
/// </summary>
[Trait("Category", "Integration")]
public sealed class AgencyOperatorServicesSqliteTests : IDisposable
{
  private const string OperatorWithoutAgencySubject = "operator-without-agency";

  private static readonly Guid CompanyId = HierarchyEndpointTestData.CompanyId;

  private readonly SqliteCoreDatabase _database = new();
  private readonly MemoryCache _memoryCache = new(new MemoryCacheOptions());

  public AgencyOperatorServicesSqliteTests()
  {
    using var db = _database.CreateDbContext();
    HierarchyEndpointTestData.Seed(db);

    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = Guid.NewGuid(),
      CompanyId = CompanyId,
      IdentitySub = OperatorWithoutAgencySubject,
      Role = Roles.AgencyOperator,
      DisplayName = "Operator Without Agency",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });
    db.SaveChanges();
  }

  public void Dispose()
  {
    _memoryCache.Dispose();
    _database.Dispose();
  }

  public static TheoryData<string?> CallersWhoAreNotActiveAgencyOperators() => new()
  {
    null,
    " ",
    HierarchyEndpointTestData.AreaManagerSubject,
    OperatorWithoutAgencySubject
  };

  // ------------------------------------------------------ Sales Rep binding

  [Theory]
  [MemberData(nameof(CallersWhoAreNotActiveAgencyOperators))]
  public async Task AssignSalesRep_CallerWhoIsNotAnActiveOperator_IsRejected(string? subject)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await SalesRepService(db, subject).AssignAsync(
      new AssignSalesRepToTerritoryRequest(HierarchyEndpointTestData.NorthTerritoryId, Guid.NewGuid()));

    result.Outcome.Should().Be(AssignSalesRepToTerritoryOutcome.CallerNotAnActiveAgencyOperator);
  }

  [Fact]
  public async Task AssignSalesRep_UnknownTerritory_IsNotFound()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await SalesRepService(db, HierarchyEndpointTestData.AgencyOperatorSubject)
      .AssignAsync(new AssignSalesRepToTerritoryRequest(Guid.NewGuid(), Guid.NewGuid()));

    result.Outcome.Should().Be(AssignSalesRepToTerritoryOutcome.TerritoryNotFound);
  }

  [Fact]
  public async Task AssignSalesRep_TerritoryOfAnotherAgency_IsRejected()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await SalesRepService(db, HierarchyEndpointTestData.AgencyOperatorSubject)
      .AssignAsync(new AssignSalesRepToTerritoryRequest(
        HierarchyEndpointTestData.SouthTerritoryId,
        Guid.NewGuid()));

    result.Outcome.Should().Be(AssignSalesRepToTerritoryOutcome.TerritoryNotAssignedToCallerAgency);
  }

  [Fact]
  public async Task AssignSalesRep_UnknownSalesRep_IsNotFound()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await SalesRepService(db, HierarchyEndpointTestData.AgencyOperatorSubject)
      .AssignAsync(new AssignSalesRepToTerritoryRequest(
        HierarchyEndpointTestData.NorthTerritoryId,
        Guid.NewGuid()));

    result.Outcome.Should().Be(AssignSalesRepToTerritoryOutcome.SalesRepNotFound);
  }

  [Fact]
  public async Task AssignSalesRep_ExistingBinding_IsIdempotent()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var northRepId = await StaffIdAsync(db, HierarchyEndpointTestData.SalesRepSubject);
    var existing = await db.SalesRepTerritoryAssignments.SingleAsync(assignment =>
      assignment.SalesRepId == northRepId && assignment.EndsAt == null);

    var result = await SalesRepService(db, HierarchyEndpointTestData.AgencyOperatorSubject)
      .AssignAsync(new AssignSalesRepToTerritoryRequest(
        HierarchyEndpointTestData.NorthTerritoryId,
        northRepId));

    result.Outcome.Should().Be(AssignSalesRepToTerritoryOutcome.Success);
    result.Assignment!.AssignmentId.Should().Be(existing.AssignmentId);
    (await db.OutboxMessages.CountAsync()).Should().Be(0, "nothing changed");
  }

  [Fact]
  public async Task AssignSalesRep_NewRep_EndsOldBindingAndRefreshesTheCache()
  {
    Guid newRepId;
    await using (var setup = _database.CreateDbContext(CompanyId))
    {
      newRepId = Guid.NewGuid();
      setup.StaffProfiles.Add(new StaffProfile
      {
        StaffProfileId = newRepId,
        CompanyId = CompanyId,
        IdentitySub = "sales-rep-new",
        Role = Roles.SalesRep,
        DisplayName = "New Rep",
        Status = HierarchyStatus.Active,
        CreatedAt = DateTimeOffset.UtcNow
      });
      await setup.SaveChangesAsync();
    }

    await using var db = _database.CreateDbContext(CompanyId);
    var cache = new MemoryRepTerritoryAssignmentCache(_memoryCache, db);
    var previousRepId = await cache.GetActiveSalesRepIdAsync(HierarchyEndpointTestData.NorthTerritoryId);

    var result = await SalesRepService(db, HierarchyEndpointTestData.AgencyOperatorSubject, cache)
      .AssignAsync(new AssignSalesRepToTerritoryRequest(
        HierarchyEndpointTestData.NorthTerritoryId,
        newRepId));

    result.Outcome.Should().Be(AssignSalesRepToTerritoryOutcome.Success);
    previousRepId.Should().NotBeNull().And.NotBe(newRepId);

    (await cache.GetActiveSalesRepIdAsync(HierarchyEndpointTestData.NorthTerritoryId))
      .Should().Be(newRepId, "the cached binding was invalidated");
    (await cache.GetActiveTerritoryIdAsync(newRepId))
      .Should().Be(HierarchyEndpointTestData.NorthTerritoryId);
    (await cache.GetActiveTerritoryIdAsync(previousRepId!.Value))
      .Should().BeNull("the previous rep's binding is now history");
  }

  // ------------------------------------------------------- Shop registration

  [Theory]
  [MemberData(nameof(CallersWhoAreNotActiveAgencyOperators))]
  public async Task RegisterShop_CallerWhoIsNotAnActiveOperator_IsRejected(string? subject)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopRegistration(db, subject, new FakeProvisioner())
      .RegisterAsync(NewShopRequest(HierarchyEndpointTestData.NorthTerritoryId));

    result.Outcome.Should().Be(RegisterShopOutcome.CallerNotAnActiveAgencyOperator);
  }

  [Fact]
  public async Task RegisterShop_UnknownTerritory_IsNotFound()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopRegistration(db, HierarchyEndpointTestData.AgencyOperatorSubject, new FakeProvisioner())
      .RegisterAsync(NewShopRequest(Guid.NewGuid()));

    result.Outcome.Should().Be(RegisterShopOutcome.TerritoryNotFound);
  }

  [Fact]
  public async Task RegisterShop_TerritoryOfAnotherAgency_IsRejected()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopRegistration(db, HierarchyEndpointTestData.AgencyOperatorSubject, new FakeProvisioner())
      .RegisterAsync(NewShopRequest(HierarchyEndpointTestData.SouthTerritoryId));

    result.Outcome.Should().Be(RegisterShopOutcome.TerritoryNotAssignedToCallerAgency);
  }

  [Fact]
  public async Task RegisterShop_WithoutOwnerEmailOrIdentity_IsRejected()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopRegistration(db, HierarchyEndpointTestData.AgencyOperatorSubject, new FakeProvisioner())
      .RegisterAsync(NewShopRequest(HierarchyEndpointTestData.NorthTerritoryId, ownerEmail: " "));

    result.Outcome.Should().Be(RegisterShopOutcome.OwnerIdentitySubRequired);
  }

  [Fact]
  public async Task RegisterShop_OwnerIdentityAlreadyLinked_IsRejected()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopRegistration(db, HierarchyEndpointTestData.AgencyOperatorSubject, new FakeProvisioner())
      .RegisterAsync(NewShopRequest(
        HierarchyEndpointTestData.NorthTerritoryId,
        ownerIdentitySub: $" {HierarchyEndpointTestData.ShopOwnerSubject} "));

    result.Outcome.Should().Be(RegisterShopOutcome.OwnerIdentityAlreadyLinked);
  }

  [Fact]
  public async Task RegisterShop_WithExplicitOwnerIdentity_LinksItWithoutProvisioning()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var provisioner = new FakeProvisioner();

    var result = await ShopRegistration(db, HierarchyEndpointTestData.AgencyOperatorSubject, provisioner)
      .RegisterAsync(NewShopRequest(
        HierarchyEndpointTestData.NorthTerritoryId,
        ownerEmail: null,
        ownerIdentitySub: "existing-owner-login"));

    result.Outcome.Should().Be(RegisterShopOutcome.Success);
    result.Shop!.OwnerIdentitySub.Should().Be("existing-owner-login");
    result.OwnerLogin.Should().BeNull();
    provisioner.Created.Should().BeEmpty();
  }

  [Fact]
  public async Task RegisterShop_OwnerEmailAlreadyHasALogin_IsAConflict()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var provisioner = new FakeProvisioner { OnCreate = user => throw new IdentityUserAlreadyExistsException(user.Email) };

    var result = await ShopRegistration(db, HierarchyEndpointTestData.AgencyOperatorSubject, provisioner)
      .RegisterAsync(NewShopRequest(HierarchyEndpointTestData.NorthTerritoryId));

    result.Outcome.Should().Be(RegisterShopOutcome.OwnerEmailAlreadyUsed);
    result.Message.Should().Contain("owner@shop.test");
  }

  [Fact]
  public async Task RegisterShop_IdentityProviderDown_IsReported()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var provisioner = new FakeProvisioner
    {
      OnCreate = _ => throw new IdentityProvisioningUnavailableException("IS is down")
    };

    var result = await ShopRegistration(db, HierarchyEndpointTestData.AgencyOperatorSubject, provisioner)
      .RegisterAsync(NewShopRequest(HierarchyEndpointTestData.NorthTerritoryId));

    result.Outcome.Should().Be(RegisterShopOutcome.IdentityProviderUnavailable);
    result.Message.Should().Be("IS is down");
  }

  [Fact]
  public async Task RegisterShop_WhenSavingFails_RemovesTheNewOwnerLogin()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var provisioner = new FakeProvisioner();

    var act = () => ShopRegistration(
        db,
        HierarchyEndpointTestData.AgencyOperatorSubject,
        provisioner,
        new BrokenOutboxWriter(db))
      .RegisterAsync(NewShopRequest(HierarchyEndpointTestData.NorthTerritoryId));

    await act.Should().ThrowAsync<DbUpdateException>();
    provisioner.Deleted.Should().Equal(provisioner.Created.Select(identity => identity.IdentityId));
  }

  // ------------------------------------------------------------- Shop update

  [Theory]
  [InlineData(null)]
  [InlineData(HierarchyEndpointTestData.AreaManagerSubject)]
  public async Task UpdateShop_CallerWhoIsNotAnActiveOperator_IsRejected(string? subject)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopUpdate(db, subject)
      .UpdateAsync(new UpdateShopRequest(HierarchyEndpointTestData.NorthShopId, 7m, 80m, 1m));

    result.Outcome.Should().Be(UpdateShopOutcome.CallerNotAnActiveAgencyOperator);
  }

  [Fact]
  public async Task UpdateShop_UnknownShop_IsNotFound()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopUpdate(db, HierarchyEndpointTestData.AgencyOperatorSubject)
      .UpdateAsync(new UpdateShopRequest(Guid.NewGuid(), 7m, 80m, 1m));

    result.Outcome.Should().Be(UpdateShopOutcome.ShopNotFound);
  }

  [Fact]
  public async Task UpdateShop_ShopOfAnotherAgency_IsRejected()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopUpdate(db, HierarchyEndpointTestData.AgencyOperatorSubject)
      .UpdateAsync(new UpdateShopRequest(HierarchyEndpointTestData.SouthShopId, 7m, 80m, 1m));

    result.Outcome.Should().Be(UpdateShopOutcome.ShopOutsideCallerAgency);
  }

  [Fact]
  public async Task UpdateShop_SameValues_ChangesNothing()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopUpdate(db, HierarchyEndpointTestData.AgencyOperatorSubject)
      .UpdateAsync(new UpdateShopRequest(HierarchyEndpointTestData.NorthShopId, 6.927079m, 79.861244m, 10000m));

    result.Outcome.Should().Be(UpdateShopOutcome.Success);
    (await db.AuditEntries.CountAsync()).Should().Be(0);
    (await db.OutboxMessages.CountAsync()).Should().Be(0);
  }

  [Fact]
  public async Task UpdateShop_OnlyCreditLimit_AuditsAndAnnouncesJustThatField()
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await ShopUpdate(db, HierarchyEndpointTestData.AgencyOperatorSubject)
      .UpdateAsync(new UpdateShopRequest(HierarchyEndpointTestData.NorthShopId, 6.927079m, 79.861244m, 20000m));

    result.Outcome.Should().Be(UpdateShopOutcome.Success);
    (await db.AuditEntries.Select(entry => entry.FieldName).ToListAsync())
      .Should().Equal("CreditLimit");
    (await db.OutboxMessages.SingleAsync()).Payload.Should().Contain("creditLimit")
      .And.NotContain("latitude");
  }

  // ------------------------------------------------------ Staff provisioning

  [Theory]
  [InlineData(null, "CompanyAdmin")]
  [InlineData("company", null)]
  [InlineData("company", "SalesRep")]
  public async Task CreateStaff_CallerWhoCannotAddStaff_IsNotAllowed(string? tenant, string? callerRole)
  {
    await using var db = _database.CreateDbContext(CompanyId);

    var result = await StaffService(db, callerRole, tenant is null ? null : CompanyId, new FakeProvisioner())
      .CreateAsync(new CreateStaffRequest(Roles.SalesRep, "Rep", "rep@example.com", null), CancellationToken.None);

    result.Outcome.Should().Be(CreateStaffOutcome.NotAllowed);
  }

  [Theory]
  [InlineData(Roles.AreaManager, "Rep", "rep@example.com", null, CreateStaffOutcome.NotAllowed)]
  [InlineData("Wizard", "Rep", "rep@example.com", null, CreateStaffOutcome.InvalidRequest)]
  [InlineData(null, "Rep", "rep@example.com", null, CreateStaffOutcome.InvalidRequest)]
  [InlineData(Roles.SalesRep, " ", "rep@example.com", null, CreateStaffOutcome.InvalidRequest)]
  [InlineData(Roles.SalesRep, "Rep", "not-an-email", null, CreateStaffOutcome.InvalidRequest)]
  [InlineData(Roles.SalesRep, "Rep", "rep@example.com", "01234567890123456789012345678901234567890", CreateStaffOutcome.InvalidRequest)]
  public async Task CreateStaff_InvalidRequestFromAnAgencyOperator_IsRejected(
    string? role,
    string displayName,
    string email,
    string? phone,
    CreateStaffOutcome expected)
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var provisioner = new FakeProvisioner();

    var result = await StaffService(db, Roles.AgencyOperator, CompanyId, provisioner)
      .CreateAsync(new CreateStaffRequest(role, displayName, email, phone), CancellationToken.None);

    result.Outcome.Should().Be(expected);
    provisioner.Created.Should().BeEmpty();
  }

  [Fact]
  public async Task CreateStaff_LoginAlreadyExistsInIdentityProvider_IsAConflict()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var provisioner = new FakeProvisioner { OnCreate = user => throw new IdentityUserAlreadyExistsException(user.Email) };

    var result = await StaffService(db, Roles.CompanyAdmin, CompanyId, provisioner)
      .CreateAsync(new CreateStaffRequest(Roles.AreaManager, "Manager", "manager@example.com", null), CancellationToken.None);

    result.Outcome.Should().Be(CreateStaffOutcome.EmailAlreadyUsed);
    result.Message.Should().Contain("manager@example.com");
  }

  [Fact]
  public async Task CreateStaff_IdentityProviderDown_IsReported()
  {
    await using var db = _database.CreateDbContext(CompanyId);
    var provisioner = new FakeProvisioner
    {
      OnCreate = _ => throw new IdentityProvisioningUnavailableException("IS is down")
    };

    var result = await StaffService(db, Roles.CompanyAdmin, CompanyId, provisioner)
      .CreateAsync(new CreateStaffRequest(Roles.AreaManager, "Manager", "manager@example.com", null), CancellationToken.None);

    result.Outcome.Should().Be(CreateStaffOutcome.IdentityProviderUnavailable);
    result.Message.Should().Be("IS is down");
  }

  [Fact]
  public async Task CreateStaff_WhenSavingFails_RemovesTheNewLogin()
  {
    // A tenant with no company row makes the profile insert fail its FK.
    var missingCompanyId = Guid.NewGuid();
    await using var db = _database.CreateDbContext(missingCompanyId);
    var provisioner = new FakeProvisioner();

    var act = () => StaffService(db, Roles.CompanyAdmin, missingCompanyId, provisioner)
      .CreateAsync(new CreateStaffRequest(Roles.SalesRep, "Rep", "rep@example.com", null), CancellationToken.None);

    await act.Should().ThrowAsync<DbUpdateException>();
    provisioner.Deleted.Should().ContainSingle()
      .Which.Should().Be(provisioner.Created.Single().IdentityId);
  }

  // ----------------------------------------------------------------- Helpers

  private static RegisterShopRequest NewShopRequest(
    Guid territoryId,
    string? ownerEmail = "owner@shop.test",
    string? ownerIdentitySub = null) =>
    new(
      territoryId,
      "Corner Shop",
      "Owner Name",
      ownerIdentitySub,
      ownerEmail,
      "0771234567",
      "1 Main Street",
      6.9m,
      79.9m,
      5000m);

  private static Task<Guid> StaffIdAsync(CoreDbContext db, string subject) =>
    db.StaffProfiles
      .Where(profile => profile.IdentitySub == subject)
      .Select(profile => profile.StaffProfileId)
      .SingleAsync();

  private SalesRepTerritoryAssignmentService SalesRepService(
    CoreDbContext db,
    string? subject,
    IRepTerritoryAssignmentCache? cache = null)
  {
    var correlation = new FixedCorrelationIdAccessor();

    return new SalesRepTerritoryAssignmentService(
      db,
      new FixedCurrentUserContext(subject, Roles.AgencyOperator),
      NullLogger<SalesRepTerritoryAssignmentService>.Instance,
      cache ?? new MemoryRepTerritoryAssignmentCache(_memoryCache, db),
      new EntityFrameworkOutboxWriter(db, correlation),
      new HierarchyEventFactory(correlation));
  }

  private static ShopRegistrationService ShopRegistration(
    CoreDbContext db,
    string? subject,
    IIdentityProvisioner provisioner,
    IOutboxWriter? outboxWriter = null)
  {
    var correlation = new FixedCorrelationIdAccessor();

    return new ShopRegistrationService(
      db,
      new FixedCurrentUserContext(subject, Roles.AgencyOperator),
      outboxWriter ?? new EntityFrameworkOutboxWriter(db, correlation),
      new HierarchyEventFactory(correlation),
      provisioner);
  }

  private static ShopUpdateService ShopUpdate(CoreDbContext db, string? subject)
  {
    var correlation = new FixedCorrelationIdAccessor();

    return new ShopUpdateService(
      db,
      new FixedCurrentUserContext(subject, Roles.AgencyOperator),
      new EntityFrameworkOutboxWriter(db, correlation),
      new HierarchyEventFactory(correlation));
  }

  private static StaffProvisioningService StaffService(
    CoreDbContext db,
    string? callerRole,
    Guid? companyId,
    IIdentityProvisioner provisioner) =>
    new(
      db,
      new FixedCurrentUserContext("caller-sub", callerRole),
      new FixedTenantContext(companyId),
      provisioner,
      NullLogger<StaffProvisioningService>.Instance);

  private sealed class FakeProvisioner : IIdentityProvisioner
  {
    public Func<NewIdentityUser, ProvisionedIdentity>? OnCreate { get; init; }

    public List<ProvisionedIdentity> Created { get; } = new();

    public List<string> Deleted { get; } = new();

    public Task<ProvisionedIdentity> CreateUserAsync(NewIdentityUser user, CancellationToken cancellationToken)
    {
      var identity = OnCreate?.Invoke(user)
        ?? new ProvisionedIdentity($"login-{Guid.NewGuid():N}", user.Email, "Temp#Pass1");
      Created.Add(identity);
      return Task.FromResult(identity);
    }

    public Task DeleteUserAsync(string identityId, CancellationToken cancellationToken)
    {
      Deleted.Add(identityId);
      return Task.CompletedTask;
    }
  }

  /// <summary>Queues an outbox row for a company that does not exist, so SaveChanges fails.</summary>
  private sealed class BrokenOutboxWriter(CoreDbContext db) : IOutboxWriter
  {
    public void Enqueue(NewOutboxMessage message) =>
      db.OutboxMessages.Add(new OutboxMessage
      {
        OutboxId = Guid.NewGuid(),
        CompanyId = Guid.NewGuid(),
        AggregateType = message.AggregateType,
        AggregateId = message.AggregateId,
        EventType = message.EventType,
        Payload = message.Payload,
        CorrelationId = "broken",
        OccurredAt = message.OccurredAt,
        NextAttemptAt = message.OccurredAt
      });
  }
}
