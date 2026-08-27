using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Shops;

public sealed class ShopRegistrationService : IShopRegistrationService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;

  public ShopRegistrationService(
    CoreDbContext db,
    ICurrentUserContext currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public async Task<RegisterShopResult> RegisterAsync(
    RegisterShopRequest request,
    CancellationToken cancellationToken = default)
  {
    var subject = _currentUser.Subject;

    if (string.IsNullOrWhiteSpace(subject))
    {
      return RegisterShopResult.CallerNotAnActiveAgencyOperator();
    }

    // Resolve the token subject to the current StaffProfile. This is a
    // database lookup on every request, rather than trusting an agency ID
    // that may have become stale after the token was issued.
    var operatorProfile = await _db.StaffProfiles
      .SingleOrDefaultAsync(
        profile =>
          profile.IdentitySub == subject &&
          profile.Role == Roles.AgencyOperator &&
          profile.Status == HierarchyStatus.Active,
        cancellationToken);

    if (operatorProfile is null)
    {
      return RegisterShopResult.CallerNotAnActiveAgencyOperator();
    }

    var operatorAgencyIds = await _db.AgencyOperatorAssignments
      .Where(assignment =>
        assignment.OperatorId == operatorProfile.StaffProfileId &&
        assignment.EndsAt == null)
      .Select(assignment => assignment.AgencyId)
      .Distinct()
      .ToListAsync(cancellationToken);

    if (operatorAgencyIds.Count == 0)
    {
      return RegisterShopResult.CallerNotAnActiveAgencyOperator();
    }

    var territory = await _db.Territories
      .SingleOrDefaultAsync(
        candidate =>
          candidate.TerritoryId == request.TerritoryId &&
          candidate.Status == HierarchyStatus.Active,
        cancellationToken);

    if (territory is null)
    {
      return RegisterShopResult.TerritoryNotFound(request.TerritoryId);
    }

    // This reads the currently active assignment at submission time.
    // A territory reassigned after operator login is therefore checked
    // against its new agency rather than the operator's old token state.
    var territoryBelongsToOperatorAgency =
      await _db.TerritoryAgencyAssignments
        .AnyAsync(
          assignment =>
            assignment.TerritoryId == territory.TerritoryId &&
            assignment.EndsAt == null &&
            operatorAgencyIds.Contains(assignment.AgencyId),
          cancellationToken);

    if (!territoryBelongsToOperatorAgency)
    {
      return RegisterShopResult.TerritoryNotAssignedToCallerAgency(
        territory.TerritoryId);
    }

    if (string.IsNullOrWhiteSpace(request.OwnerIdentitySub))
    {
      return RegisterShopResult.OwnerIdentitySubRequired();
    }

    var ownerIdentitySub = request.OwnerIdentitySub.Trim();

    var ownerIdentityAlreadyLinked = await _db.Shops
      .AnyAsync(
        shop => shop.OwnerIdentitySub == ownerIdentitySub,
        cancellationToken);

    if (ownerIdentityAlreadyLinked)
    {
      return RegisterShopResult.OwnerIdentityAlreadyLinked(
        ownerIdentitySub);
    }

    var shop = new Shop
    {
      ShopId = Guid.NewGuid(),
      CompanyId = territory.CompanyId,
      TerritoryId = territory.TerritoryId,
      Name = request.Name.Trim(),
      OwnerName = request.OwnerName?.Trim(),
      OwnerIdentitySub = ownerIdentitySub,
      OwnerEmail = request.OwnerEmail?.Trim(),
      OwnerPhone = request.OwnerPhone?.Trim(),
      Address = request.Address.Trim(),
      Latitude = request.Latitude,
      Longitude = request.Longitude,
      CreditLimit = request.CreditLimit,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };

    _db.Shops.Add(shop);
    await _db.SaveChangesAsync(cancellationToken);

    return RegisterShopResult.Success(shop);
  }
}