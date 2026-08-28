namespace Sellora.CoreService.Application.ProvinceAssignments;

/// <summary>
/// Operations on the append-only province-manager assignment history.
/// CompanyAdmin restriction is enforced by the endpoint policy; this
/// service enforces the data-level guards a policy cannot: the target
/// exists in the caller's company and holds the AreaManager role.
/// </summary>
public interface IProvinceAssignmentService
{
  /// <summary>
  /// Assigns an Area Manager to a province with no active manager.
  /// The transactional end-then-create reassignment (AC2) is delivered
  /// in CSP-63.
  /// </summary>
  Task<AssignAreaManagerResult> AssignAreaManagerAsync(
    AssignAreaManagerRequest request,
    CancellationToken cancellationToken = default);

  Task<UpdateAreaManagerReportsToResult> UpdateAreaManagerReportsToAsync(
    UpdateAreaManagerReportsToRequest request,
    CancellationToken cancellationToken = default);
}
