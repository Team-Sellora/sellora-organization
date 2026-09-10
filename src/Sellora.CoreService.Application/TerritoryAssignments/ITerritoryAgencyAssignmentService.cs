namespace Sellora.CoreService.Application.TerritoryAssignments;

public interface ITerritoryAgencyAssignmentService
{
  Task<AssignTerritoryAgencyResult> AssignAsync(
    AssignTerritoryAgencyRequest request,
    CancellationToken cancellationToken = default);
}