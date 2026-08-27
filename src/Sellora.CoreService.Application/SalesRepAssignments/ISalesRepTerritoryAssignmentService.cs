namespace Sellora.CoreService.Application.SalesRepAssignments;

public interface ISalesRepTerritoryAssignmentService
{
  Task<AssignSalesRepToTerritoryResult> AssignAsync(
    AssignSalesRepToTerritoryRequest request,
    CancellationToken cancellationToken = default);
}