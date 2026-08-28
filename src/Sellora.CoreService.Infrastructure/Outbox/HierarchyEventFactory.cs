using System.Text.Json;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Infrastructure.Outbox;

public sealed class HierarchyEventFactory : IHierarchyEventFactory
{
  private const string SchemaVersion = "1.0";

  private readonly ICorrelationIdAccessor _correlationIdAccessor;

  public HierarchyEventFactory(
    ICorrelationIdAccessor correlationIdAccessor)
  {
    _correlationIdAccessor = correlationIdAccessor;
  }

  public NewOutboxMessage AgencyRegistered(
    Agency agency,
    Guid operatorId)
  {
    return Create(
      agency.CompanyId,
      "Agency",
      agency.AgencyId,
      "AgencyRegistered",
      agency.CreatedAt,
      (eventId, correlationId) => new
      {
        eventId,
        eventType = "AgencyRegistered",
        schemaVersion = SchemaVersion,
        companyId = agency.CompanyId,
        entityId = agency.AgencyId,
        agencyId = agency.AgencyId,
        provinceId = agency.ProvinceId,
        operatorId,
        effectiveAt = agency.CreatedAt,
        correlationId
      });
  }

  public NewOutboxMessage TerritoryAssignedToAgency(
    Territory territory,
    TerritoryAgencyAssignment assignment)
  {
    return Create(
      territory.CompanyId,
      "Territory",
      territory.TerritoryId,
      "TerritoryAssignedToAgency",
      assignment.StartsAt,
      (eventId, correlationId) => new
      {
        eventId,
        eventType = "TerritoryAssignedToAgency",
        schemaVersion = SchemaVersion,
        companyId = territory.CompanyId,
        entityId = territory.TerritoryId,
        territoryId = territory.TerritoryId,
        agencyId = assignment.AgencyId,
        provinceId = territory.ProvinceId,
        effectiveAt = assignment.StartsAt,
        correlationId
      });
  }

  public NewOutboxMessage ShopRegistered(
    Shop shop,
    Guid agencyId)
  {
    return Create(
      shop.CompanyId,
      "Shop",
      shop.ShopId,
      "ShopRegistered",
      shop.CreatedAt,
      (eventId, correlationId) => new
      {
        eventId,
        eventType = "ShopRegistered",
        schemaVersion = SchemaVersion,
        companyId = shop.CompanyId,
        entityId = shop.ShopId,
        shopId = shop.ShopId,
        agencyId,
        territoryId = shop.TerritoryId,
        effectiveAt = shop.CreatedAt,
        correlationId
      });
  }

  public NewOutboxMessage ShopUpdated(
    Shop shop,
    Guid agencyId,
    IReadOnlyCollection<string> changedFields)
  {
    var effectiveAt = shop.UpdatedAt ?? DateTimeOffset.UtcNow;

    return Create(
      shop.CompanyId,
      "Shop",
      shop.ShopId,
      "ShopUpdated",
      effectiveAt,
      (eventId, correlationId) => new
      {
        eventId,
        eventType = "ShopUpdated",
        schemaVersion = SchemaVersion,
        companyId = shop.CompanyId,
        entityId = shop.ShopId,
        shopId = shop.ShopId,
        agencyId,
        territoryId = shop.TerritoryId,
        changedFields,
        effectiveAt,
        correlationId
      });
  }

  public NewOutboxMessage SalesRepAssigned(
    SalesRepTerritoryAssignment assignment,
    Guid agencyId)
  {
    return Create(
      assignment.CompanyId,
      "SalesRep",
      assignment.SalesRepId,
      "SalesRepAssigned",
      assignment.StartsAt,
      (eventId, correlationId) => new
      {
        eventId,
        eventType = "SalesRepAssigned",
        schemaVersion = SchemaVersion,
        companyId = assignment.CompanyId,
        entityId = assignment.SalesRepId,
        salesRepId = assignment.SalesRepId,
        territoryId = assignment.TerritoryId,
        agencyId,
        effectiveAt = assignment.StartsAt,
        correlationId
      });
  }

  public NewOutboxMessage HierarchyEntityDeactivated(
    Agency agency,
    DateTimeOffset effectiveAt)
  {
    return Create(
      agency.CompanyId,
      "Agency",
      agency.AgencyId,
      "HierarchyEntityDeactivated",
      effectiveAt,
      (eventId, correlationId) => new
      {
        eventId,
        eventType = "HierarchyEntityDeactivated",
        schemaVersion = SchemaVersion,
        companyId = agency.CompanyId,
        entityId = agency.AgencyId,
        entityType = "Agency",
        effectiveAt,
        correlationId
      });
  }

  private NewOutboxMessage Create(
    Guid companyId,
    string aggregateType,
    Guid aggregateId,
    string eventType,
    DateTimeOffset occurredAt,
    Func<Guid, string, object> payloadFactory)
  {
    var eventId = Guid.NewGuid();
    var correlationId = _correlationIdAccessor.GetCorrelationId();

    return new NewOutboxMessage(
      companyId,
      aggregateType,
      aggregateId,
      eventType,
      SchemaVersion,
      JsonSerializer.Serialize(payloadFactory(eventId, correlationId)),
      occurredAt);
  }
}