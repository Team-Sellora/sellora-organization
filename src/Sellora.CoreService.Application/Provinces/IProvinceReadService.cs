namespace Sellora.CoreService.Application.Provinces;

/// <summary>
/// Read-side queries over the province list. Kept separate from
/// IProvinceAssignmentService so command and query surfaces don't grow
/// entangled — the assignment service performs writes and rich validation,
/// this one is a pure projection.
/// </summary>
public interface IProvinceReadService
{
  /// <summary>
  /// Lists provinces in the caller's company. Each row includes the current
  /// active Area Manager (or null) and active agency/shop counts. Backed by
  /// a single aggregate query.
  /// </summary>
  Task<IReadOnlyList<ProvinceSummaryResponse>> ListAsync(
    CancellationToken cancellationToken = default);
}