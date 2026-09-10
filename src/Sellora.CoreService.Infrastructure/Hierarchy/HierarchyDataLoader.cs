using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Hierarchy;

/// <summary>
/// Loads active hierarchy rows allowed by a resolved visibility scope.
/// All queries also pass through CoreDbContext's company tenant filter.
/// </summary>
internal sealed class HierarchyDataLoader
{
  private readonly CoreDbContext _db;

  public HierarchyDataLoader(CoreDbContext db)
  {
    _db = db;
  }

  public async Task<HierarchyDataSet> LoadAsync(
    HierarchyVisibilityScope scope,
    CancellationToken cancellationToken)
  {
    var provinces = await LoadProvincesAsync(
      scope,
      cancellationToken);

    var provinceIds = provinces
      .Select(province => province.ProvinceId)
      .ToHashSet();

    var agencies = await LoadAgenciesAsync(
      scope,
      provinceIds,
      cancellationToken);

    var agencyIds = agencies
      .Select(agency => agency.AgencyId)
      .ToHashSet();

    var territories = await LoadTerritoriesAsync(
      scope,
      provinceIds,
      cancellationToken);

    var territoryIds = territories
      .Select(territory => territory.TerritoryId)
      .ToHashSet();

    var assignments = await LoadAssignmentsAsync(
      territoryIds,
      agencyIds,
      cancellationToken);

    var shops = await LoadShopsAsync(
      scope,
      territoryIds,
      cancellationToken);

    return new HierarchyDataSet(
      provinces,
      agencies,
      territories,
      assignments,
      shops);
  }

  private async Task<List<Province>> LoadProvincesAsync(
    HierarchyVisibilityScope scope,
    CancellationToken cancellationToken)
  {
    var query = _db.Provinces
      .AsNoTracking()
      .Where(province =>
        province.Status == HierarchyStatus.Active);

    if (scope.ProvinceIds is not null)
    {
      query = query.Where(province =>
        scope.ProvinceIds.Contains(province.ProvinceId));
    }

    return await query
      .OrderBy(province => province.Name)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<Agency>> LoadAgenciesAsync(
    HierarchyVisibilityScope scope,
    HashSet<Guid> visibleProvinceIds,
    CancellationToken cancellationToken)
  {
    var query = _db.Agencies
      .AsNoTracking()
      .Where(agency =>
        agency.Status == HierarchyStatus.Active &&
        visibleProvinceIds.Contains(agency.ProvinceId));

    if (scope.AgencyIds is not null)
    {
      query = query.Where(agency =>
        scope.AgencyIds.Contains(agency.AgencyId));
    }

    return await query
      .OrderBy(agency => agency.Name)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<Territory>>
    LoadTerritoriesAsync(
      HierarchyVisibilityScope scope,
      HashSet<Guid> visibleProvinceIds,
      CancellationToken cancellationToken)
  {
    var query = _db.Territories
      .AsNoTracking()
      .Where(territory =>
        territory.Status == HierarchyStatus.Active &&
        visibleProvinceIds.Contains(territory.ProvinceId));

    if (scope.TerritoryIds is not null)
    {
      query = query.Where(territory =>
        scope.TerritoryIds.Contains(
          territory.TerritoryId));
    }

    return await query
      .OrderBy(territory => territory.Name)
      .ToListAsync(cancellationToken);
  }

  private async Task<List<TerritoryAgencyAssignment>>
    LoadAssignmentsAsync(
      HashSet<Guid> visibleTerritoryIds,
      HashSet<Guid> visibleAgencyIds,
      CancellationToken cancellationToken)
  {
    return await _db.TerritoryAgencyAssignments
      .AsNoTracking()
      .Where(assignment =>
        assignment.EndsAt == null &&
        visibleTerritoryIds.Contains(
          assignment.TerritoryId) &&
        visibleAgencyIds.Contains(
          assignment.AgencyId))
      .ToListAsync(cancellationToken);
  }

  private async Task<List<Shop>> LoadShopsAsync(
    HierarchyVisibilityScope scope,
    HashSet<Guid> visibleTerritoryIds,
    CancellationToken cancellationToken)
  {
    var query = _db.Shops
      .AsNoTracking()
      .Where(shop =>
        shop.Status == HierarchyStatus.Active &&
        visibleTerritoryIds.Contains(shop.TerritoryId));

    if (scope.ShopIds is not null)
    {
      query = query.Where(shop =>
        scope.ShopIds.Contains(shop.ShopId));
    }

    return await query
      .OrderBy(shop => shop.Name)
      .ToListAsync(cancellationToken);
  }
}