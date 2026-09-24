using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.IdentityProvisioning;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using Sellora.CoreService.Application.Outbox;

namespace Sellora.CoreService.Infrastructure.Shops;

public sealed class ShopRegistrationService : IShopRegistrationService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly IOutboxWriter _outboxWriter;
  private readonly IHierarchyEventFactory _hierarchyEventFactory;
  private readonly IIdentityProvisioner _provisioner;
  public ShopRegistrationService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    IOutboxWriter outboxWriter,
    IHierarchyEventFactory hierarchyEventFactory,
    IIdentityProvisioner provisioner)
  {
    _db = db;
    _currentUser = currentUser;
    _outboxWriter = outboxWriter;
    _hierarchyEventFactory = hierarchyEventFactory;
    _provisioner = provisioner;
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

    var agencyId = await _db.TerritoryAgencyAssignments
      .Where(assignment =>
        assignment.TerritoryId == territory.TerritoryId &&
        assignment.EndsAt == null)
      .Select(assignment => assignment.AgencyId)
      .SingleAsync(cancellationToken);

    // Normally the Shop Owner's login is created here from their email, so
    // nobody copies an identity ID by hand. An existing login can still be
    // linked by passing ownerIdentitySub explicitly.
    var ownerEmail = string.IsNullOrWhiteSpace(request.OwnerEmail)
      ? null
      : request.OwnerEmail.Trim().ToLowerInvariant();
    var explicitSub = string.IsNullOrWhiteSpace(request.OwnerIdentitySub)
      ? null
      : request.OwnerIdentitySub.Trim();

    if (explicitSub is null && ownerEmail is null)
    {
      return RegisterShopResult.OwnerIdentitySubRequired();
    }

    if (explicitSub is not null &&
        await _db.Shops.AnyAsync(shop => shop.OwnerIdentitySub == explicitSub, cancellationToken))
    {
      return RegisterShopResult.OwnerIdentityAlreadyLinked(explicitSub);
    }

    ProvisionedIdentity? provisioned = null;

    if (explicitSub is null)
    {
      try
      {
        provisioned = await _provisioner.CreateUserAsync(
          new NewIdentityUser(
            ownerEmail!,
            string.IsNullOrWhiteSpace(request.OwnerName) ? request.Name.Trim() : request.OwnerName.Trim(),
            request.OwnerPhone,
            Roles.ShopOwner,
            territory.CompanyId),
          cancellationToken);
      }
      catch (IdentityUserAlreadyExistsException)
      {
        return RegisterShopResult.OwnerEmailAlreadyUsed(ownerEmail!);
      }
      catch (IdentityProvisioningUnavailableException exception)
      {
        return RegisterShopResult.IdentityProviderUnavailable(exception.Message);
      }
    }

    var ownerIdentitySub = explicitSub ?? provisioned!.IdentityId;

    var shop = new Shop
    {
      ShopId = Guid.NewGuid(),
      CompanyId = territory.CompanyId,
      TerritoryId = territory.TerritoryId,
      Name = request.Name.Trim(),
      OwnerName = request.OwnerName?.Trim(),
      OwnerIdentitySub = ownerIdentitySub,
      OwnerEmail = ownerEmail,
      OwnerPhone = request.OwnerPhone?.Trim(),
      Address = request.Address.Trim(),
      Latitude = request.Latitude,
      Longitude = request.Longitude,
      CreditLimit = request.CreditLimit,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };

    _db.Shops.Add(shop);

    _outboxWriter.Enqueue(
      _hierarchyEventFactory.ShopRegistered(shop, agencyId));

    try
    {
      await _db.SaveChangesAsync(CancellationToken.None);
    }
    catch
    {
      if (provisioned is not null)
      {
        await _provisioner.DeleteUserAsync(provisioned.IdentityId, CancellationToken.None);
      }

      throw;
    }

    return RegisterShopResult.Success(
      shop,
      provisioned is null
        ? null
        : new ProvisionedShopOwner(provisioned.IdentityId, provisioned.UserName, provisioned.TemporaryPassword));
  }
}