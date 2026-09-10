using Sellora.CoreService.Application.TerritoryAssignments;

namespace Sellora.CoreService.Infrastructure.TerritoryAssignments;

/// <summary>
/// Sprint 1 placeholder. Replace this with an order/delivery-backed
/// implementation when those services become available.
/// </summary>
public sealed class StubOpenWorkChecker : IOpenWorkChecker
{
  public Task<OpenWorkResult> GetOpenWorkForTerritoryAsync(Guid territoryId, CancellationToken cancellationToken = default) => Task.FromResult(new OpenWorkResult(HasOpenWork: false, BlockingReferences: Array.Empty<string>()));
}