using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.SalesRepAssignments;

public sealed class RepShopRelationshipVerifier
  : IRepShopRelationshipVerifier
{
  private readonly CoreDbContext _db;
  private readonly IRepTerritoryAssignmentCache _assignmentCache;

  public RepShopRelationshipVerifier(
    CoreDbContext db,
    IRepTerritoryAssignmentCache assignmentCache)
  {
    _db = db;
    _assignmentCache = assignmentCache;
  }

  public async Task<VerifyRepShopRelationshipResponse> VerifyAsync(
    Guid salesRepId,
    Guid shopId,
    CancellationToken cancellationToken = default)
  {

    var activeTerritoryId =
      await _assignmentCache.GetActiveTerritoryIdAsync(
        salesRepId,
        cancellationToken);

    // One database round trip. PostgreSQL can use the shop primary key and
    // active-territory partial index for the correlated EXISTS check.
    var shop = await _db.Shops
      .AsNoTracking()
      .Where(candidate => candidate.ShopId == shopId)
      .Select(candidate => new
      {
        candidate.Status,
        TerritoryIsActive = _db.Territories.Any(territory =>
          territory.TerritoryId == candidate.TerritoryId &&
          territory.Status == HierarchyStatus.Active),
        RepCoversTerritory = activeTerritoryId == candidate.TerritoryId
      })
      .SingleOrDefaultAsync(cancellationToken);

    if (shop is null)
    {
      return VerifyRepShopRelationshipResponse.Invalid("shopNotFound");
    }

    if (shop.Status != HierarchyStatus.Active ||
        !shop.TerritoryIsActive)
    {
      return VerifyRepShopRelationshipResponse.Invalid("shopInactive");
    }

    return shop.RepCoversTerritory
      ? VerifyRepShopRelationshipResponse.Valid()
      : VerifyRepShopRelationshipResponse.Invalid(
        "repNotAssignedToShopTerritory");
  }
}