using Sellora.CoreService.Application.Territories;

namespace Sellora.CoreService.Application.SalesRepAssignments;

public sealed record SalesRepSummary(
  Guid SalesRepId,
  string DisplayName,
  string? Email,
  string Status,
  TerritoryResponse? CurrentTerritory);

public interface ISalesRepAssignmentReadService
{
  Task<IReadOnlyList<SalesRepSummary>> ListAsync(
    CancellationToken cancellationToken = default);

  Task<IReadOnlyList<TerritoryResponse>> ListUnassignedTerritoriesAsync(
    CancellationToken cancellationToken = default);
}