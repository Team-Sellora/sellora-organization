namespace Sellora.CoreService.Application.SalesRepAssignments;

public sealed record AssignSalesRepToTerritoryRequest(
  Guid TerritoryId,
  Guid SalesRepId);