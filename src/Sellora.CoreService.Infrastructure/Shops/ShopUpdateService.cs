using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;
using Sellora.CoreService.Application.Outbox;

namespace Sellora.CoreService.Infrastructure.Shops;

public sealed class ShopUpdateService : IShopUpdateService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly IOutboxWriter _outboxWriter;
  private readonly IHierarchyEventFactory _hierarchyEventFactory;
  public ShopUpdateService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    IOutboxWriter outboxWriter,
    IHierarchyEventFactory hierarchyEventFactory)
  {
    _db = db;
    _currentUser = currentUser;
    _outboxWriter = outboxWriter;
    _hierarchyEventFactory = hierarchyEventFactory;
  }

  public async Task<UpdateShopResult> UpdateAsync(
    UpdateShopRequest request,
    CancellationToken cancellationToken = default)
  {
    var subject = _currentUser.Subject;

    if (string.IsNullOrWhiteSpace(subject))
    {
      return UpdateShopResult.CallerNotAnActiveAgencyOperator();
    }

    var operatorProfile = await _db.StaffProfiles
      .SingleOrDefaultAsync(
        profile =>
          profile.IdentitySub == subject &&
          profile.Role == Roles.AgencyOperator &&
          profile.Status == HierarchyStatus.Active,
        cancellationToken);

    if (operatorProfile is null)
    {
      return UpdateShopResult.CallerNotAnActiveAgencyOperator();
    }

    var operatorAgencyIds = await _db.AgencyOperatorAssignments
      .Where(assignment =>
        assignment.OperatorId == operatorProfile.StaffProfileId &&
        assignment.EndsAt == null)
      .Select(assignment => assignment.AgencyId)
      .Distinct()
      .ToListAsync(cancellationToken);

    var shop = await _db.Shops.SingleOrDefaultAsync(
      candidate =>
        candidate.ShopId == request.ShopId &&
        candidate.Status == HierarchyStatus.Active,
      cancellationToken);

    if (shop is null)
    {
      return UpdateShopResult.ShopNotFound(request.ShopId);
    }

    var ownsShopTerritory = await _db.TerritoryAgencyAssignments.AnyAsync(
      assignment =>
        assignment.TerritoryId == shop.TerritoryId &&
        assignment.EndsAt == null &&
        operatorAgencyIds.Contains(assignment.AgencyId),
      cancellationToken);

    if (!ownsShopTerritory)
    {
      return UpdateShopResult.ShopOutsideCallerAgency(shop.ShopId);
    }

    var agencyId = await _db.TerritoryAgencyAssignments
      .Where(assignment =>
        assignment.TerritoryId == shop.TerritoryId &&
        assignment.EndsAt == null)
      .Select(assignment => assignment.AgencyId)
      .SingleAsync(cancellationToken);

    var coordinatesChanged =
      shop.Latitude != request.Latitude ||
      shop.Longitude != request.Longitude;

    var creditLimitChanged = shop.CreditLimit != request.CreditLimit;

    if (!coordinatesChanged && !creditLimitChanged)
    {
      return UpdateShopResult.Success(shop);
    }

    var now = DateTimeOffset.UtcNow;

    await using var transaction = await _db.Database.BeginTransactionAsync(
      cancellationToken);

    // Add the audit rows before changing the current values. Both audit rows
    // and the shop update are committed together by one SaveChangesAsync.
    if (coordinatesChanged)
    {
      _db.AuditEntries.Add(new AuditEntry
      {
        AuditEntryId = Guid.NewGuid(),
        CompanyId = shop.CompanyId,
        EntityType = "Shop",
        EntityId = shop.ShopId,
        FieldName = "Coordinates",
        OldValue = JsonSerializer.Serialize(new
        {
          latitude = shop.Latitude,
          longitude = shop.Longitude
        }),
        NewValue = JsonSerializer.Serialize(new
        {
          latitude = request.Latitude,
          longitude = request.Longitude
        }),
        ChangedBy = subject,
        ChangedAt = now
      });
    }

    if (creditLimitChanged)
    {
      _db.AuditEntries.Add(new AuditEntry
      {
        AuditEntryId = Guid.NewGuid(),
        CompanyId = shop.CompanyId,
        EntityType = "Shop",
        EntityId = shop.ShopId,
        FieldName = "CreditLimit",
        OldValue = JsonSerializer.Serialize(shop.CreditLimit),
        NewValue = JsonSerializer.Serialize(request.CreditLimit),
        ChangedBy = subject,
        ChangedAt = now
      });
    }

    shop.Latitude = request.Latitude;
    shop.Longitude = request.Longitude;
    shop.CreditLimit = request.CreditLimit;
    shop.UpdatedAt = now;

    var changedFields = new List<string>();

    if (coordinatesChanged)
    {
      changedFields.Add("latitude");
      changedFields.Add("longitude");
    }

    if (creditLimitChanged)
    {
      changedFields.Add("creditLimit");
    }

    _outboxWriter.Enqueue(
      _hierarchyEventFactory.ShopUpdated(
        shop,
        agencyId,
        changedFields));

    await _db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return UpdateShopResult.Success(shop);
  }
}