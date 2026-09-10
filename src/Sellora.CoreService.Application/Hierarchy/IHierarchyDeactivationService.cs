namespace Sellora.CoreService.Application.Hierarchy;

/// <summary>
/// Provides supported hierarchy deactivation operations.
///
/// There is intentionally no delete operation: hierarchy rows must remain
/// available to orders, payments, audits, and assignment history.
/// </summary>
public interface IHierarchyDeactivationService
{
  /// <summary>
  /// Deactivates an agency belonging to the caller's company.
  /// Returns false when the agency is not visible in the caller's tenant.
  /// </summary>
  Task<bool> DeactivateAgencyAsync(
    Guid agencyId,
    CancellationToken cancellationToken = default);
}