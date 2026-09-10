namespace Sellora.CoreService.Application.TerritoryAssignments;

public interface IOpenWorkChecker
{
  Task<OpenWorkResult> GetOpenWorkForTerritoryAsync(Guid territoryId, CancellationToken cancellationToken = default);
}

public sealed record OpenWorkResult(bool HasOpenWork, IReadOnlyList<string> BlockingReferences);