namespace Sellora.CoreService.Application.TerritoryAssignments;

public sealed record AssignTerritoryAgencyRequest(Guid TerritoryId, Guid AgencyId);