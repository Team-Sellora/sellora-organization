namespace Sellora.CoreService.Application.AreaManagers;

/// <summary>
/// Lists StaffProfiles that hold the AreaManager role — the population
/// source for the "assign manager" dropdown.
/// </summary>
public interface IAreaManagerReadService
{
  Task<IReadOnlyList<AreaManagerSummary>> ListActiveAsync(
    CancellationToken cancellationToken = default);
}