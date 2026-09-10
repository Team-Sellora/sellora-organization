namespace Sellora.CoreService.Application.Hierarchy;

public interface IHierarchyReadService
{
  Task<HierarchyTreeResponse?> GetHierarchyAsync(
    CancellationToken cancellationToken = default);
}