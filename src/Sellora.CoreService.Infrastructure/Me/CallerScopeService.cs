using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.Me;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Domain.Tenancy;
using Sellora.CoreService.Infrastructure.Hierarchy;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Me;

/// <summary>
/// GET /api/me/scope. Reuses HierarchyScopeResolver — the same logic that
/// already decides what each role can see — so there is one definition of
/// "where does this user sit", not two.
/// </summary>
public sealed class CallerScopeService : ICallerScopeService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly ITenantContext _tenant;

  public CallerScopeService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ITenantContext tenant)
  {
    _db = db;
    _currentUser = currentUser;
    _tenant = tenant;
  }

  public async Task<CallerScopeResult> GetAsync(
    CancellationToken cancellationToken)
  {
    var subject = _currentUser.Subject;
    var role = _currentUser.Role;
    var companyId = _tenant.CompanyId;

    if (string.IsNullOrWhiteSpace(subject) ||
        string.IsNullOrWhiteSpace(role) ||
        companyId is null)
    {
      return CallerScopeResult.NotAuthenticated();
    }

    StaffProfile? profile = null;

    if (role != Roles.ShopOwner)
    {
      profile = await _db.StaffProfiles
        .AsNoTracking()
        .SingleOrDefaultAsync(
          candidate =>
            candidate.IdentitySub == subject &&
            candidate.Role == role &&
            candidate.Status == HierarchyStatus.Active,
          cancellationToken);

      // A Company Admin sees the whole company, so a missing profile does
      // not block them; every other role is meaningless without one.
      if (profile is null && role != Roles.CompanyAdmin)
      {
        return CallerScopeResult.ProfileNotFound(role);
      }
    }

    var visibility = await new HierarchyScopeResolver(_db, _currentUser)
      .ResolveAsync(cancellationToken);

    if (role == Roles.ShopOwner && visibility.ShopIds is { Count: 0 })
    {
      return CallerScopeResult.ProfileNotFound(role);
    }

    var provinceIds = Sorted(visibility.ProvinceIds);
    var agencyIds = Sorted(visibility.AgencyIds);
    var territoryIds = Sorted(visibility.TerritoryIds);
    var shopIds = Sorted(visibility.ShopIds);

    return CallerScopeResult.Found(new CallerScopeResponse(
      subject,
      companyId.Value,
      role,
      profile?.StaffProfileId,
      profile?.DisplayName,
      SalesRepId: role == Roles.SalesRep ? profile?.StaffProfileId : null,
      AgencyId: SingleOrNull(agencyIds),
      TerritoryId: role == Roles.SalesRep ? SingleOrNull(territoryIds) : null,
      ShopId: role == Roles.ShopOwner ? SingleOrNull(shopIds) : null,
      provinceIds,
      agencyIds,
      territoryIds,
      shopIds));
  }

  // Null means "unrestricted" in HierarchyVisibilityScope; for this
  // response an unrestricted caller simply has no specific IDs listed.
  private static IReadOnlyList<Guid> Sorted(HashSet<Guid>? ids) =>
    ids is null ? Array.Empty<Guid>() : ids.OrderBy(id => id).ToList();

  private static Guid? SingleOrNull(IReadOnlyList<Guid> ids) =>
    ids.Count == 1 ? ids[0] : null;
}
