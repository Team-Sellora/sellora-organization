using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Shops;

public sealed class ShopReadService : IShopReadService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;

  public ShopReadService(
    CoreDbContext db,
    ICurrentUserContext currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public async Task<PagedResponse<ShopResponse>> ListAsync(
    ShopListQuery query,
    CancellationToken cancellationToken = default)
  {
    var subject = _currentUser.Subject;

    if (string.IsNullOrWhiteSpace(subject))
    {
      return new PagedResponse<ShopResponse>(
        Array.Empty<ShopResponse>(),
        0,
        query.Page,
        query.PageSize);
    }

    var operatorId = await _db.StaffProfiles
      .AsNoTracking()
      .Where(profile =>
        profile.IdentitySub == subject &&
        profile.Role == Roles.AgencyOperator &&
        profile.Status == HierarchyStatus.Active)
      .Select(profile => (Guid?)profile.StaffProfileId)
      .SingleOrDefaultAsync(cancellationToken);

    if (operatorId is null)
    {
      return new PagedResponse<ShopResponse>(
        Array.Empty<ShopResponse>(),
        0,
        query.Page,
        query.PageSize);
    }

    var agencyIds = await _db.AgencyOperatorAssignments
      .AsNoTracking()
      .Where(assignment =>
        assignment.OperatorId == operatorId.Value &&
        assignment.EndsAt == null)
      .Select(assignment => assignment.AgencyId)
      .ToListAsync(cancellationToken);

    var territoryIds = await _db.TerritoryAgencyAssignments
      .AsNoTracking()
      .Where(assignment =>
        assignment.EndsAt == null &&
        agencyIds.Contains(assignment.AgencyId))
      .Select(assignment => assignment.TerritoryId)
      .ToListAsync(cancellationToken);

    var shops = _db.Shops
      .AsNoTracking()
      .Where(shop =>
        territoryIds.Contains(shop.TerritoryId) &&
        shop.Status == query.Status);

    if (query.TerritoryId is not null)
    {
      shops = shops.Where(shop =>
        shop.TerritoryId == query.TerritoryId.Value);
    }

    var totalCount = await shops.CountAsync(cancellationToken);

    var items = await shops
      .OrderBy(shop => shop.Name)
      .ThenBy(shop => shop.ShopId)
      .Skip((query.Page - 1) * query.PageSize)
      .Take(query.PageSize)
      .Select(shop => new ShopResponse(
        shop.ShopId,
        shop.TerritoryId,
        shop.Name,
        shop.OwnerName,
        shop.OwnerEmail,
        shop.OwnerPhone,
        shop.Address,
        shop.Latitude,
        shop.Longitude,
        shop.CreditLimit,
        shop.Status,
        shop.CreatedAt,
        shop.UpdatedAt))
      .ToListAsync(cancellationToken);

    return new PagedResponse<ShopResponse>(
      items,
      totalCount,
      query.Page,
      query.PageSize);
  }
}