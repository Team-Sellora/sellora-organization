using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Common;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Agencies;

public sealed class AgencyReadService : IAgencyReadService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly ILogger<AgencyReadService> _logger;

  public AgencyReadService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ILogger<AgencyReadService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _logger = logger;
  }

  public async Task<PagedResponse<AgencyResponse>> ListAsync(
    AgencyListQuery query,
    CancellationToken cancellationToken = default)
  {
    // Step 1: resolve the caller from the JWT. If they don't resolve to
    // an active AreaManager staff profile, silent-empty rather than 403
    // — a list endpoint's contract is "here is what you can see", and an
    // unresolvable caller simply sees nothing. The warning is the audit
    // trail for anyone investigating unexpected empty responses.
    var callerSub = _currentUser.Subject;
    if (string.IsNullOrEmpty(callerSub))
    {
      _logger.LogWarning(
        "GET /api/agencies returning empty: request carried no subject claim.");
      return PagedResponse<AgencyResponse>.Empty(query.Page, query.PageSize);
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
        "GET /api/agencies returning empty: JWT subject {Subject} does " +
        "not resolve to an active AreaManager staff profile.",
        callerSub);
      return PagedResponse<AgencyResponse>.Empty(query.Page, query.PageSize);
    }

    // Step 2: read the caller's *current* managed province IDs from the
    // assignment table. Live check on every request — reassignments take
    // effect immediately, no cached claim can spoof scope.
    var managedProvinceIds = await _db.ProvinceManagerAssignments
      .AsNoTracking()
      .Where(a =>
        a.AreaManagerId == callerProfile.StaffProfileId &&
        a.EndsAt == null)
      .Select(a => a.ProvinceId)
      .ToListAsync(cancellationToken);

    if (managedProvinceIds.Count == 0)
    {
      // AM with no active assignments — nothing in scope. Short-circuit
      // so we don't build an "IN ()" clause that Postgres will complain
      // about and so we don't waste a round-trip.
      return PagedResponse<AgencyResponse>.Empty(query.Page, query.PageSize);
    }

    // Step 3: build the filtered query. Scope FIRST (managed provinces),
    // THEN status, THEN the optional client-supplied provinceId filter.
    // A client-supplied provinceId outside the managed set collapses the
    // intersection to empty naturally — no special-case needed, and no
    // information leaks about whether that province exists.
    var q = _db.Agencies
      .AsNoTracking()
      .Where(a => managedProvinceIds.Contains(a.ProvinceId))
      .Where(a => a.Status == query.Status);

    if (query.ProvinceId is Guid filterProvinceId)
    {
      q = q.Where(a => a.ProvinceId == filterProvinceId);
    }

    // Step 4: two-query pagination — one COUNT, one page. Simple,
    // portable, and the row counts here (hundreds per company) don't
    // justify a window-function optimisation.
    var totalCount = await q.CountAsync(cancellationToken);

    var items = await q
      .OrderBy(a => a.Name)
      .ThenBy(a => a.AgencyId)
      .Skip((query.Page - 1) * query.PageSize)
      .Take(query.PageSize)
      .Select(a => new AgencyResponse(
        a.AgencyId,
        a.ProvinceId,
        a.Name,
        a.Email,
        a.Phone,
        a.Address,
        a.Status,
        a.CreatedAt))
      .ToListAsync(cancellationToken);

    return new PagedResponse<AgencyResponse>(
      items,
      query.Page,
      query.PageSize,
      totalCount);
  }
}