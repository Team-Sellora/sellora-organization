using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Agencies;
using Sellora.CoreService.Infrastructure.Persistence;
using Xunit;
using Sellora.CoreService.Tests;

namespace Sellora.CoreService.Tests.Agencies;

/// <summary>
/// Scoping tests for AgencyReadService. Runs against real Postgres via
/// the shared fixture — an in-memory provider would honour the LINQ
/// filters, but the point of this suite is end-to-end proof, which means
/// exercising the same query the production request would.
/// </summary>
public sealed class AgencyReadServiceTests
  : IClassFixture<PostgreSqlConstraintFixture>, IAsyncLifetime
{
  private readonly PostgreSqlConstraintFixture _fixture;

  private readonly Guid _companyId = Guid.NewGuid();
  private readonly Guid _westernProvinceId = Guid.NewGuid();
  private readonly Guid _centralProvinceId = Guid.NewGuid();
  private readonly Guid _southernProvinceId = Guid.NewGuid();
  private readonly Guid _amProfileId = Guid.NewGuid();
  private readonly string _amSub = $"test-sub:area-manager:{Guid.NewGuid():N}";

  private readonly Guid _westernAgencyId = Guid.NewGuid();
  private readonly Guid _centralAgencyId = Guid.NewGuid();
  private readonly Guid _southernAgencyId = Guid.NewGuid();
  private readonly Guid _westernInactiveAgencyId = Guid.NewGuid();

  public AgencyReadServiceTests(PostgreSqlConstraintFixture fixture)
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

    // Three provinces in ONE company. AM will manage two of them.
    db.Provinces.AddRange(
      NewProvince(_westernProvinceId, "WP", "Western"),
      NewProvince(_centralProvinceId, "CP", "Central"),
      NewProvince(_southernProvinceId, "SP", "Southern"));

    db.StaffProfiles.Add(new StaffProfile
    {
      StaffProfileId = _amProfileId,
      CompanyId = _companyId,
      IdentitySub = _amSub,
      Role = Roles.AreaManager,
      DisplayName = "Test AM",
      Email = "am@test.local",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });

    // AM assigned to Western and Central — NOT Southern.
    db.ProvinceManagerAssignments.AddRange(
      NewManagerAssignment(_westernProvinceId),
      NewManagerAssignment(_centralProvinceId));

    // Four agencies: one active in each province, plus one inactive in
    // Western (should be excluded by the default status=Active filter).
    db.Agencies.AddRange(
      NewAgency(_westernAgencyId, _westernProvinceId, "Western Agency",
        HierarchyStatus.Active),
      NewAgency(_centralAgencyId, _centralProvinceId, "Central Agency",
        HierarchyStatus.Active),
      NewAgency(_southernAgencyId, _southernProvinceId, "Southern Agency",
        HierarchyStatus.Active),
      NewAgency(_westernInactiveAgencyId, _westernProvinceId,
        "Deactivated Western Agency", HierarchyStatus.Inactive));

    await db.SaveChangesAsync();
  }

  public Task DisposeAsync() => Task.CompletedTask;

  /// <summary>
  /// The headline CSP-70 requirement: AM with two managed provinces sees
  /// only agencies in those two provinces, never in the third.
  /// </summary>
  [Fact]
  public async Task ListAsync_AmWithTwoManagedProvinces_ReturnsOnlyThoseProvinces()
  {
    var currentUser = new FakeCurrentUserContext(_amSub);
    await using var db = _fixture.CreateDbContext(_companyId);
    var service = new AgencyReadService(
      db,
      currentUser,
      NullLogger<AgencyReadService>.Instance);

    var result = await service.ListAsync(new AgencyListQuery(
      Status: HierarchyStatus.Active,
      ProvinceId: null,
      Page: 1,
      PageSize: 25));

    // Two active agencies in managed provinces; Southern is out of scope;
    // the inactive one is excluded by the default status filter.
    result.TotalCount.Should().Be(2);
    result.Items.Should().HaveCount(2);

    var returnedProvinceIds = result.Items
      .Select(i => i.ProvinceId)
      .ToHashSet();
    returnedProvinceIds.Should().BeEquivalentTo(new[]
    {
      _westernProvinceId,
      _centralProvinceId
    });

    result.Items.Should().NotContain(i => i.AgencyId == _southernAgencyId,
      "Southern is not managed by the caller");
    result.Items.Should().NotContain(i => i.AgencyId == _westernInactiveAgencyId,
      "the inactive agency must be excluded by the default status=Active");
  }

  /// <summary>
  /// A provinceId filter outside the caller's scope must collapse to
  /// empty — no leak about whether that province exists in the company.
  /// </summary>
  [Fact]
  public async Task ListAsync_ProvinceIdOutsideScope_ReturnsEmpty()
  {
    var currentUser = new FakeCurrentUserContext(_amSub);
    await using var db = _fixture.CreateDbContext(_companyId);
    var service = new AgencyReadService(
      db,
      currentUser,
      NullLogger<AgencyReadService>.Instance);

    var result = await service.ListAsync(new AgencyListQuery(
      Status: HierarchyStatus.Active,
      ProvinceId: _southernProvinceId,  // exists in company, NOT managed
      Page: 1,
      PageSize: 25));

    result.TotalCount.Should().Be(0);
    result.Items.Should().BeEmpty();
  }

  /// <summary>
  /// Status filter round-trip — flipping to Inactive returns the
  /// deactivated agency and NOT the active ones.
  /// </summary>
  [Fact]
  public async Task ListAsync_StatusInactive_ReturnsOnlyInactiveInScope()
  {
    var currentUser = new FakeCurrentUserContext(_amSub);
    await using var db = _fixture.CreateDbContext(_companyId);
    var service = new AgencyReadService(
      db,
      currentUser,
      NullLogger<AgencyReadService>.Instance);

    var result = await service.ListAsync(new AgencyListQuery(
      Status: HierarchyStatus.Inactive,
      ProvinceId: null,
      Page: 1,
      PageSize: 25));

    result.TotalCount.Should().Be(1);
    result.Items.Should().ContainSingle()
      .Which.AgencyId.Should().Be(_westernInactiveAgencyId);
  }

  private Province NewProvince(Guid id, string code, string name) => new()
  {
    ProvinceId = id,
    CompanyId = _companyId,
    Code = code,
    Name = name,
    Status = HierarchyStatus.Active,
    CreatedAt = DateTimeOffset.UtcNow
  };

  private ProvinceManagerAssignment NewManagerAssignment(Guid provinceId) => new()
  {
    AssignmentId = Guid.NewGuid(),
    CompanyId = _companyId,
    ProvinceId = provinceId,
    AreaManagerId = _amProfileId,
    StartsAt = DateTimeOffset.UtcNow,
    CreatedBy = "test"
  };

  private Agency NewAgency(
    Guid id,
    Guid provinceId,
    string name,
    string status) => new()
    {
      AgencyId = id,
      CompanyId = _companyId,
      ProvinceId = provinceId,
      Name = name,
      Status = status,
      CreatedAt = DateTimeOffset.UtcNow
    };

  private sealed class FakeCurrentUserContext : ICurrentUserContext
  {
    public FakeCurrentUserContext(string subject) { Subject = subject; }
    public string? Subject { get; }
    public string? Role => Roles.AreaManager;
  }
}