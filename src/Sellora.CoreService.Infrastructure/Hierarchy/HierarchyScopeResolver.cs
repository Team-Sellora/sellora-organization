using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Hierarchy;

/// <summary>
/// Converts the authenticated user's role and subject into the hierarchy
/// identifiers that user is allowed to view.
///
/// Company isolation is still enforced separately by CoreDbContext's global
/// tenant filter using the company ID from the JWT.
/// </summary>
internal sealed class HierarchyScopeResolver
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;

  public HierarchyScopeResolver(
    CoreDbContext db,
    ICurrentUserContext currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public async Task<HierarchyVisibilityScope> ResolveAsync(
    CancellationToken cancellationToken)
  {
    var role = _currentUser.Role;
    var subject = _currentUser.Subject;

    if (role == "CompanyAdmin")
    {
      return HierarchyVisibilityScope.All;
    }

    if (string.IsNullOrWhiteSpace(subject))
    {
      return HierarchyVisibilityScope.None;
    }

    return role switch
    {
      "AreaManager" =>
        await ResolveAreaManagerAsync(
          subject,
          cancellationToken),

      "AgencyOperator" =>
        await ResolveAgencyOperatorAsync(
          subject,
          cancellationToken),

      "SalesRep" =>
        await ResolveSalesRepAsync(
          subject,
          cancellationToken),

      "ShopOwner" =>
        await ResolveShopOwnerAsync(
          subject,
          cancellationToken),

      _ => HierarchyVisibilityScope.None
    };
  }

  private async Task<HierarchyVisibilityScope>
    ResolveAreaManagerAsync(
      string subject,
      CancellationToken cancellationToken)
  {
    var staffId = await FindActiveStaffIdAsync(
      subject,
      "AreaManager",
      cancellationToken);

    if (staffId is null)
    {
      return HierarchyVisibilityScope.None;
    }

    var provinceIds =
      await _db.ProvinceManagerAssignments
        .AsNoTracking()
        .Where(assignment =>
          assignment.AreaManagerId == staffId.Value &&
          assignment.EndsAt == null)
        .Select(assignment => assignment.ProvinceId)
        .ToHashSetAsync(cancellationToken);

    // Area Managers see everything contained by their assigned provinces.
    return new HierarchyVisibilityScope(
      ProvinceIds: provinceIds,
      AgencyIds: null,
      TerritoryIds: null,
      ShopIds: null);
  }

  private async Task<HierarchyVisibilityScope>
    ResolveAgencyOperatorAsync(
      string subject,
      CancellationToken cancellationToken)
  {
    var staffId = await FindActiveStaffIdAsync(
      subject,
      "AgencyOperator",
      cancellationToken);

    if (staffId is null)
    {
      return HierarchyVisibilityScope.None;
    }

    var agencyIds =
      await _db.AgencyOperatorAssignments
        .AsNoTracking()
        .Where(assignment =>
          assignment.OperatorId == staffId.Value &&
          assignment.EndsAt == null)
        .Select(assignment => assignment.AgencyId)
        .ToHashSetAsync(cancellationToken);

    var provinceIds = await _db.Agencies
      .AsNoTracking()
      .Where(agency =>
        agency.Status == HierarchyStatus.Active &&
        agencyIds.Contains(agency.AgencyId))
      .Select(agency => agency.ProvinceId)
      .ToHashSetAsync(cancellationToken);

    var territoryIds =
      await _db.TerritoryAgencyAssignments
        .AsNoTracking()
        .Where(assignment =>
          assignment.EndsAt == null &&
          agencyIds.Contains(assignment.AgencyId))
        .Select(assignment => assignment.TerritoryId)
        .ToHashSetAsync(cancellationToken);

    return new HierarchyVisibilityScope(
      provinceIds,
      agencyIds,
      territoryIds,
      ShopIds: null);
  }

  private async Task<HierarchyVisibilityScope>
    ResolveSalesRepAsync(
      string subject,
      CancellationToken cancellationToken)
  {
    var staffId = await FindActiveStaffIdAsync(
      subject,
      "SalesRep",
      cancellationToken);

    if (staffId is null)
    {
      return HierarchyVisibilityScope.None;
    }

    var territoryIds =
      await _db.SalesRepTerritoryAssignments
        .AsNoTracking()
        .Where(assignment =>
          assignment.SalesRepId == staffId.Value &&
          assignment.EndsAt == null)
        .Select(assignment => assignment.TerritoryId)
        .ToHashSetAsync(cancellationToken);

    var provinceIds = await _db.Territories
      .AsNoTracking()
      .Where(territory =>
        territory.Status == HierarchyStatus.Active &&
        territoryIds.Contains(territory.TerritoryId))
      .Select(territory => territory.ProvinceId)
      .ToHashSetAsync(cancellationToken);

    var agencyIds =
      await _db.TerritoryAgencyAssignments
        .AsNoTracking()
        .Where(assignment =>
          assignment.EndsAt == null &&
          territoryIds.Contains(assignment.TerritoryId))
        .Select(assignment => assignment.AgencyId)
        .ToHashSetAsync(cancellationToken);

    return new HierarchyVisibilityScope(
      provinceIds,
      agencyIds,
      territoryIds,
      ShopIds: null);
  }

  private async Task<HierarchyVisibilityScope>
    ResolveShopOwnerAsync(
      string subject,
      CancellationToken cancellationToken)
  {
    var shopIds = await _db.Shops
      .AsNoTracking()
      .Where(shop =>
        shop.Status == HierarchyStatus.Active &&
        shop.OwnerIdentitySub == subject)
      .Select(shop => shop.ShopId)
      .ToHashSetAsync(cancellationToken);

    var territoryIds = await _db.Shops
      .AsNoTracking()
      .Where(shop => shopIds.Contains(shop.ShopId))
      .Select(shop => shop.TerritoryId)
      .ToHashSetAsync(cancellationToken);

    var provinceIds = await _db.Territories
      .AsNoTracking()
      .Where(territory =>
        territory.Status == HierarchyStatus.Active &&
        territoryIds.Contains(territory.TerritoryId))
      .Select(territory => territory.ProvinceId)
      .ToHashSetAsync(cancellationToken);

    var agencyIds =
      await _db.TerritoryAgencyAssignments
        .AsNoTracking()
        .Where(assignment =>
          assignment.EndsAt == null &&
          territoryIds.Contains(assignment.TerritoryId))
        .Select(assignment => assignment.AgencyId)
        .ToHashSetAsync(cancellationToken);

    return new HierarchyVisibilityScope(
      provinceIds,
      agencyIds,
      territoryIds,
      shopIds);
  }

  private async Task<Guid?> FindActiveStaffIdAsync(
    string subject,
    string expectedRole,
    CancellationToken cancellationToken)
  {
    return await _db.StaffProfiles
      .AsNoTracking()
      .Where(profile =>
        profile.IdentitySub == subject &&
        profile.Role == expectedRole &&
        profile.Status == HierarchyStatus.Active)
      .Select(profile =>
        (Guid?)profile.StaffProfileId)
      .SingleOrDefaultAsync(cancellationToken);
  }
}