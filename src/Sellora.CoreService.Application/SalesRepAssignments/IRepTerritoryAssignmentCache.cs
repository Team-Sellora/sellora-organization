namespace Sellora.CoreService.Application.SalesRepAssignments;

public interface IRepTerritoryAssignmentCache
{
  Task<Guid?> GetActiveTerritoryIdAsync(
    Guid salesRepId,
    CancellationToken cancellationToken = default);

  Task<Guid?> GetActiveSalesRepIdAsync(
    Guid territoryId,
    CancellationToken cancellationToken = default);

  void Invalidate(Guid salesRepId, Guid territoryId);
}