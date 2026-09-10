using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Application.Outbox;

public interface IHierarchyEventFactory
{
  NewOutboxMessage AgencyRegistered(
    Agency agency,
    Guid operatorId);

  NewOutboxMessage TerritoryAssignedToAgency(
    Territory territory,
    TerritoryAgencyAssignment assignment);

  NewOutboxMessage ShopRegistered(
    Shop shop,
    Guid agencyId);

  NewOutboxMessage ShopUpdated(
    Shop shop,
    Guid agencyId,
    IReadOnlyCollection<string> changedFields);

  NewOutboxMessage SalesRepAssigned(
    SalesRepTerritoryAssignment assignment,
    Guid agencyId);

  NewOutboxMessage HierarchyEntityDeactivated(
    Agency agency,
    DateTimeOffset effectiveAt);
}