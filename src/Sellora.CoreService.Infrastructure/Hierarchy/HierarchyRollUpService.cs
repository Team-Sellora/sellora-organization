using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Hierarchy;

public sealed class HierarchyRollUpService : IHierarchyRollUpService
{
  private readonly CoreDbContext _db;

  public HierarchyRollUpService(CoreDbContext db)
  {
    _db = db;
  }

  public async Task<IReadOnlyList<ProvinceRollUpResponse>> ListAsync(
    CancellationToken cancellationToken = default)
  {
    // This remains a single database round trip. EF translates every count
    // and manager lookup below into a correlated subquery on Provinces rather
    // than issuing one query per province.
    var rows = await _db.Provinces
      .AsNoTracking()
      .OrderBy(province => province.Name)
      .ThenBy(province => province.ProvinceId)
      .Select(province => new
      {
        province.ProvinceId,
        province.Code,
        province.Name,
        province.Status,
        Manager = _db.ProvinceManagerAssignments
          .Where(assignment =>
            assignment.ProvinceId == province.ProvinceId &&
            assignment.EndsAt == null)
          .Join(
            _db.StaffProfiles,
            assignment => assignment.AreaManagerId,
            profile => profile.StaffProfileId,
            (assignment, profile) => new
            {
              profile.StaffProfileId,
              profile.DisplayName,
              profile.Email
            })
          .FirstOrDefault(),
        ReportsToAdmin = _db.ProvinceManagerAssignments
          .Where(assignment =>
            assignment.ProvinceId == province.ProvinceId &&
            assignment.EndsAt == null &&
            assignment.ReportsToAdminId != null)
          .Join(
            _db.StaffProfiles,
            assignment => assignment.ReportsToAdminId,
            profile => (Guid?)profile.StaffProfileId,
            (assignment, profile) => new
            {
              profile.StaffProfileId,
              profile.DisplayName,
              profile.Email
            })
          .FirstOrDefault(),
        AgencyCount = _db.Agencies.Count(agency =>
          agency.ProvinceId == province.ProvinceId &&
          agency.Status == HierarchyStatus.Active),
        TerritoryCount = _db.Territories.Count(territory =>
          territory.ProvinceId == province.ProvinceId &&
          territory.Status == HierarchyStatus.Active),
        ShopCount = _db.Shops.Count(shop =>
          shop.Status == HierarchyStatus.Active &&
          _db.Territories.Any(territory =>
            territory.TerritoryId == shop.TerritoryId &&
            territory.ProvinceId == province.ProvinceId &&
            territory.Status == HierarchyStatus.Active)),
        UnassignedTerritoryCount = _db.Territories.Count(territory =>
          territory.ProvinceId == province.ProvinceId &&
          territory.Status == HierarchyStatus.Active &&
          !_db.TerritoryAgencyAssignments.Any(assignment =>
            assignment.TerritoryId == territory.TerritoryId &&
            assignment.EndsAt == null))
      })
      .ToListAsync(cancellationToken);

    return rows
      .Select(row => new ProvinceRollUpResponse(
        row.ProvinceId,
        row.Code,
        row.Name,
        row.Status,
        row.Manager is null
          ? null
          : new AreaManagerRollUpSummary(
              row.Manager.StaffProfileId,
              row.Manager.DisplayName,
              row.Manager.Email,
              row.ReportsToAdmin is null
                ? null
                : new ReportingAdminRollUpSummary(
                    row.ReportsToAdmin.StaffProfileId,
                    row.ReportsToAdmin.DisplayName,
                    row.ReportsToAdmin.Email)),
        row.AgencyCount,
        row.TerritoryCount,
        row.ShopCount,
        row.UnassignedTerritoryCount,
        row.UnassignedTerritoryCount > 0))
      .ToList();
  }
}
