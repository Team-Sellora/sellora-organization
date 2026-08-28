using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Hierarchy;
using Sellora.CoreService.Infrastructure.Outbox;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public sealed class HierarchyDeactivationTests
  : IClassFixture<PostgreSqlConstraintFixture>
{
  private readonly PostgreSqlConstraintFixture _fixture;

  public HierarchyDeactivationTests(
    PostgreSqlConstraintFixture fixture)
  {
    _fixture = fixture;
  }

  [Fact]
  public async Task DeactivateAgency_PreservesHierarchyReferences()
  {
    var companyId = Guid.NewGuid();

    await using var db =
      _fixture.CreateDbContext(companyId);

    var seed = await SeedAgencyHierarchyAsync(
      db,
      companyId);

    var correlationAccessor = new FakeCorrelationIdAccessor();
    var outboxWriter = new EntityFrameworkOutboxWriter(db, correlationAccessor);
    var eventFactory = new HierarchyEventFactory(correlationAccessor);
    var service = new HierarchyDeactivationService(db, outboxWriter, eventFactory);

    var result = await service.DeactivateAgencyAsync(
      seed.AgencyId);

    Assert.True(result);

    db.ChangeTracker.Clear();

    var agency = await db.Agencies
      .SingleAsync(candidate =>
        candidate.AgencyId == seed.AgencyId);

    Assert.Equal(
      HierarchyStatus.Inactive,
      agency.Status);

    // The inactive agency is excluded when an active-list query is used.
    var appearsInActiveList = await db.Agencies
      .AnyAsync(candidate =>
        candidate.AgencyId == seed.AgencyId &&
        candidate.Status == HierarchyStatus.Active);

    Assert.False(appearsInActiveList);

    // The agency row still exists and remains queryable.
    var agencyStillExists = await db.Agencies
      .IgnoreQueryFilters()
      .AnyAsync(candidate =>
        candidate.AgencyId == seed.AgencyId);

    Assert.True(agencyStillExists);

    // The historical agency-to-territory relationship remains intact.
    var assignmentStillExists =
      await db.TerritoryAgencyAssignments
        .IgnoreQueryFilters()
        .AnyAsync(assignment =>
          assignment.AssignmentId ==
            seed.TerritoryAgencyAssignmentId &&
          assignment.AgencyId == seed.AgencyId &&
          assignment.TerritoryId == seed.TerritoryId);

    Assert.True(assignmentStillExists);

    // The territory remains intact.
    var territoryStillExists = await db.Territories
      .IgnoreQueryFilters()
      .AnyAsync(territory =>
        territory.TerritoryId == seed.TerritoryId);

    Assert.True(territoryStillExists);

    // The associated shop remains attached to its territory.
    var shopStillExists = await db.Shops
      .IgnoreQueryFilters()
      .AnyAsync(shop =>
        shop.ShopId == seed.ShopId &&
        shop.TerritoryId == seed.TerritoryId);

    Assert.True(shopStillExists);
  }

  [Fact]
  public async Task HardDeleteAgency_IsBlockedByDbContext()
  {
    var companyId = Guid.NewGuid();

    await using var db =
      _fixture.CreateDbContext(companyId);

    var seed = await SeedAgencyHierarchyAsync(
      db,
      companyId);

    // Clear the change tracker so dependent entities (like TerritoryAgencyAssignment)
    // are not tracked. Otherwise, db.Agencies.Remove() throws immediately because
    // of DeleteBehavior.Restrict before SaveChangesAsync is even called.
    db.ChangeTracker.Clear();

    var agency = await db.Agencies
      .SingleAsync(candidate =>
        candidate.AgencyId == seed.AgencyId);

    db.Agencies.Remove(agency);

    var exception =
      await Assert.ThrowsAsync<InvalidOperationException>(
        async () => await db.SaveChangesAsync());

    Assert.Contains(
      "Hard deletion is intentionally unsupported",
      exception.Message);

    db.ChangeTracker.Clear();

    var agencyStillExists = await db.Agencies
      .IgnoreQueryFilters()
      .AnyAsync(candidate =>
        candidate.AgencyId == seed.AgencyId);

    Assert.True(agencyStillExists);
  }

  [Fact]
  public async Task DeactivateAgency_FromAnotherTenant_ReturnsNotFound()
  {
    var ownerCompanyId = Guid.NewGuid();
    var otherCompanyId = Guid.NewGuid();

    AgencyHierarchySeed seed;

    await using (var ownerDb =
      _fixture.CreateDbContext(ownerCompanyId))
    {
      seed = await SeedAgencyHierarchyAsync(
        ownerDb,
        ownerCompanyId);
    }

    await using var otherTenantDb =
      _fixture.CreateDbContext(otherCompanyId);

    var correlationAccessor = new FakeCorrelationIdAccessor();
    var outboxWriter = new EntityFrameworkOutboxWriter(otherTenantDb, correlationAccessor);
    var eventFactory = new HierarchyEventFactory(correlationAccessor);
    var service =
      new HierarchyDeactivationService(otherTenantDb, outboxWriter, eventFactory);

    var result = await service.DeactivateAgencyAsync(
      seed.AgencyId);

    Assert.False(result);

    await using var verificationDb =
      _fixture.CreateDbContext(ownerCompanyId);

    var agency = await verificationDb.Agencies
      .SingleAsync(candidate =>
        candidate.AgencyId == seed.AgencyId);

    Assert.Equal(
      HierarchyStatus.Active,
      agency.Status);
  }

  private static async Task<AgencyHierarchySeed>
    SeedAgencyHierarchyAsync(
      CoreDbContext db,
      Guid companyId)
  {
    var provinceId = Guid.NewGuid();
    var agencyId = Guid.NewGuid();
    var territoryId = Guid.NewGuid();
    var assignmentId = Guid.NewGuid();
    var shopId = Guid.NewGuid();
    var suffix = Guid.NewGuid().ToString("N")[..8];
    var now = DateTimeOffset.UtcNow;

    db.AddRange(
      new Company
      {
        CompanyId = companyId,
        TenantCode = $"tenant-{suffix}",
        Name = $"Test Company {suffix}",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Province
      {
        ProvinceId = provinceId,
        CompanyId = companyId,
        Code = $"P-{suffix}",
        Name = $"Test Province {suffix}",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Agency
      {
        AgencyId = agencyId,
        CompanyId = companyId,
        ProvinceId = provinceId,
        Name = $"Test Agency {suffix}",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new Territory
      {
        TerritoryId = territoryId,
        CompanyId = companyId,
        ProvinceId = provinceId,
        Code = $"T-{suffix}",
        Name = $"Test Territory {suffix}",
        Status = HierarchyStatus.Active,
        CreatedAt = now
      },
      new TerritoryAgencyAssignment
      {
        AssignmentId = assignmentId,
        CompanyId = companyId,
        TerritoryId = territoryId,
        AgencyId = agencyId,
        StartsAt = now,
        CreatedBy = "deactivation-test"
      },
      new Shop
      {
        ShopId = shopId,
        CompanyId = companyId,
        TerritoryId = territoryId,
        Name = $"Test Shop {suffix}",
        Address = "123 Test Road",
        Latitude = 6.927079m,
        Longitude = 79.861244m,
        CreditLimit = 10000.00m,
        Status = HierarchyStatus.Active,
        CreatedAt = now
      });

    await db.SaveChangesAsync();

    return new AgencyHierarchySeed(
      agencyId,
      territoryId,
      assignmentId,
      shopId);
  }

  private sealed record AgencyHierarchySeed(
    Guid AgencyId,
    Guid TerritoryId,
    Guid TerritoryAgencyAssignmentId,
    Guid ShopId);

  private sealed class FakeCorrelationIdAccessor : ICorrelationIdAccessor
  {
    public string GetCorrelationId() => "test-correlation-id";
  }
}