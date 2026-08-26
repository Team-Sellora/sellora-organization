using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Provinces;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Provinces;

public sealed class ProvinceReadService : IProvinceReadService
{
  private readonly CoreDbContext _db;

  public ProvinceReadService(CoreDbContext db)
  {
    _db = db;
  }

  public async Task<IReadOnlyList<ProvinceSummaryResponse>> ListAsync(
    CancellationToken cancellationToken = default)
  {
    // Every query on ITenantScoped entities is automatically filtered by
    // company via CoreDbContext's global query filter, so the caller's
    // company from the JWT scopes the whole result — nothing extra to do.

    // Single aggregate projection. The subqueries for manager and the two
    // counts translate to correlated subqueries on the outer Provinces
    // query, so this is one SQL round trip. Anonymous type first so EF's
    // projection is uncontroversial; the record mapping runs in memory
    // over a small list.
    var rows = await _db.Provinces
      .AsNoTracking()
      .OrderBy(p => p.Name)
      .Select(p => new
      {
        p.ProvinceId,
        p.Code,
        p.Name,
        p.Status,
        p.CreatedAt,
        Manager = _db.ProvinceManagerAssignments
          .Where(a =>
            a.ProvinceId == p.ProvinceId &&
            a.EndsAt == null)
          .Join(
            _db.StaffProfiles,
            a => a.AreaManagerId,
            s => s.StaffProfileId,
            (a, s) => new
            {
              s.StaffProfileId,
              s.DisplayName,
              s.Email
            })
          .FirstOrDefault(),
        AgencyCount = _db.Agencies
          .Count(a =>
            a.ProvinceId == p.ProvinceId &&
            a.Status == HierarchyStatus.Active),
        ShopCount = _db.Shops
          .Count(s =>
            s.Status == HierarchyStatus.Active &&
            _db.Territories.Any(t =>
              t.TerritoryId == s.TerritoryId &&
              t.ProvinceId == p.ProvinceId))
      })
      .ToListAsync(cancellationToken);

    return rows
      .Select(r => new ProvinceSummaryResponse(
        r.ProvinceId,
        r.Code,
        r.Name,
        r.Status,
        r.CreatedAt,
        r.Manager is null
          ? null
          : new CurrentManagerSummary(
              r.Manager.StaffProfileId,
              r.Manager.DisplayName,
              r.Manager.Email),
        r.AgencyCount,
        r.ShopCount))
      .ToList();
  }
}