using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sellora.CoreService.Application.Common;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Territories;

public sealed class TerritoryReadService : ITerritoryReadService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly ILogger<TerritoryReadService> _logger;

  public TerritoryReadService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ILogger<TerritoryReadService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _logger = logger;
  }

  public async Task<PagedResponse<TerritoryResponse>> ListAsync(
    TerritoryListQuery query,
    CancellationToken cancellationToken = default)
  {
    var callerSub = _currentUser.Subject;
    if (string.IsNullOrEmpty(callerSub))
    {
      _logger.LogWarning(
        "GET /api/territories returning empty: request carried no " +
        "subject claim.");
      return PagedResponse<TerritoryResponse>.Empty(query.Page, query.PageSize);
    }

    var callerProfile = await _db.StaffProfiles
      .AsNoTracking()
      .SingleOrDefaultAsync(
        s => s.IdentitySub == callerSub &&
             s.Role == Roles.AreaManager &&
             s.Status == HierarchyStatus.Active,
        cancellationToken);

    if (callerProfile is null)
    {
      _logger.LogWarning(
        "GET /api/territories returning empty: JWT subject {Subject} " +
        "does not resolve to an active AreaManager staff profile.",
        callerSub);
      return PagedResponse<TerritoryResponse>.Empty(query.Page, query.PageSize);
    }

    var managedProvinceIds = await _db.ProvinceManagerAssignments
      .AsNoTracking()
      .Where(a =>
        a.AreaManagerId == callerProfile.StaffProfileId &&
        a.EndsAt == null)
      .Select(a => a.ProvinceId)
      .ToListAsync(cancellationToken);

    if (managedProvinceIds.Count == 0)
    {
      return PagedResponse<TerritoryResponse>.Empty(query.Page, query.PageSize);
    }

    var q = _db.Territories
      .AsNoTracking()
      .Where(t => managedProvinceIds.Contains(t.ProvinceId))
      .Where(t => t.Status == query.Status);

    if (query.ProvinceId is Guid filterProvinceId)
    {
      q = q.Where(t => t.ProvinceId == filterProvinceId);
    }

    var totalCount = await q.CountAsync(cancellationToken);

    // Sort by Code — territory codes are the operational identifier
    // (WP-T-01 etc), so alphabetical-by-code is what an operator scanning
    // a list expects, more so than by name.
    var items = await q
      .OrderBy(t => t.Code)
      .ThenBy(t => t.TerritoryId)
      .Skip((query.Page - 1) * query.PageSize)
      .Take(query.PageSize)
      .Select(t => new TerritoryResponse(
        t.TerritoryId,
        t.ProvinceId,
        t.Code,
        t.Name,
        t.GeographicDescription,
        t.Status,
        t.CreatedAt))
      .ToListAsync(cancellationToken);

    return new PagedResponse<TerritoryResponse>(
      items,
      query.Page,
      query.PageSize,
      totalCount);
  }
}