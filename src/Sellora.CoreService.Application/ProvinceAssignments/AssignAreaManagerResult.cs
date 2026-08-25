using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;

namespace Sellora.CoreService.Application.ProvinceAssignments;

/// <summary>Named outcomes of an assign-area-manager attempt.</summary>
public enum AssignAreaManagerOutcome
{
  /// <summary>Assignment created. Maps to 200 OK.</summary>
  Success,

  /// <summary>The province was not found within the caller's company. Maps to 404.</summary>
  ProvinceNotFound,

  /// <summary>The target user was not found within the caller's company. Maps to 400.</summary>
  TargetUserNotFound,

  /// <summary>The target user exists but does not hold the AreaManager role. Maps to 400.</summary>
  TargetNotAreaManager,

  /// <summary>The target user is deactivated. Maps to 400.</summary>
  TargetInactive,

  /// <summary>
  /// The province already has an active manager. Maps to 409.
  /// CSP-63 replaces this branch with the transactional end-then-create
  /// reassignment logic (append-only history, AC2).
  /// </summary>
  ProvinceAlreadyHasActiveManager
}

/// <summary>
/// Result of an assign-area-manager attempt. Carries a stable Outcome for
/// control flow and a human-readable Message that names the specific problem,
/// so the React screen can surface a clear inline error instead of a generic
/// failure.
/// </summary>
public sealed class AssignAreaManagerResult
{
  public AssignAreaManagerOutcome Outcome { get; }
  public string Message { get; }
  public ProvinceManagerAssignment? Assignment { get; }

  private AssignAreaManagerResult(
    AssignAreaManagerOutcome outcome,
    string message,
    ProvinceManagerAssignment? assignment = null)
  {
    Outcome = outcome;
    Message = message;
    Assignment = assignment;
  }

  public bool IsSuccess => Outcome == AssignAreaManagerOutcome.Success;

  public static AssignAreaManagerResult Success(
    ProvinceManagerAssignment assignment) =>
    new(AssignAreaManagerOutcome.Success,
      "Assignment created.",
      assignment);

  public static AssignAreaManagerResult ProvinceNotFound(Guid provinceId) =>
    new(AssignAreaManagerOutcome.ProvinceNotFound,
      $"Province '{provinceId}' was not found in your company.");

  public static AssignAreaManagerResult TargetUserNotFound(Guid staffProfileId) =>
    new(AssignAreaManagerOutcome.TargetUserNotFound,
      $"User '{staffProfileId}' was not found in your company.");

  public static AssignAreaManagerResult TargetNotAreaManager(
    StaffProfile target) =>
    new(AssignAreaManagerOutcome.TargetNotAreaManager,
      $"User '{target.StaffProfileId}' holds the '{target.Role}' role, " +
      $"not '{Roles.AreaManager}'. " +
      "Only Area Managers can be assigned to a province.");

  public static AssignAreaManagerResult TargetInactive(StaffProfile target) =>
    new(AssignAreaManagerOutcome.TargetInactive,
      $"User '{target.StaffProfileId}' is inactive and cannot be " +
      "assigned to a province.");

  public static AssignAreaManagerResult ProvinceAlreadyHasActiveManager(
    Guid provinceId) =>
    new(AssignAreaManagerOutcome.ProvinceAlreadyHasActiveManager,
      $"Province '{provinceId}' already has an active area manager. " +
      "Reassignment (ending the prior assignment) is delivered in CSP-63.");
}