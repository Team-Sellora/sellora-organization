namespace Sellora.CoreService.Application.Hierarchy;

public interface IHierarchyRollUpService
{
  Task<IReadOnlyList<ProvinceRollUpResponse>> ListAsync(
    CancellationToken cancellationToken = default);
}
