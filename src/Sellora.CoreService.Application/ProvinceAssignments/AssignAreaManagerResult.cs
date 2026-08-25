using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;

namespace Sellora.CoreService.Application.ProvinceAssignments;

/// <summary>Named outcomes of an assign-area-manager attempt.</summary>
public enum AssignAreaManagerOutcome
{
  /// <summary>Assignment created — or already the active one (idempotent). Maps to 200.</summary>
  Success,

  /// <summary>Province not found in the caller's company. Maps to 404.</summary>
  ProvinceNotFound,

  /// <summary>Target user not found in the caller's company. Maps to 400.</summary>
  TargetUserNotFound,

  /// <summary>Target user does not hold the AreaManager role. Maps to 400.</summary>
  TargetNotAreaManager,

  /// <summary>Target user is deactivated. Maps to 400.</summary>
  TargetInactive,

  /// <summary>
  /// Target user is already the active Area Manager of a different province.
  /// Maps to 409. An Area Manager may cover multiple provinces, but that must
  /// be an intentional decision — this endpoint flags it rather than silently
  /// spreading a person across provinces.
  /// </summary>
  TargetAlreadyManagesAnotherProvince
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
    new(AssignAreaManagerOutcome.Success, "Assignment created.", assignment);

  public static AssignAreaManagerResult ProvinceNotFound(Guid provinceId) =>
    new(AssignAreaManagerOutcome.ProvinceNotFound,
      $"Province '{provinceId}' was not found in your company.");

  public static AssignAreaManagerResult TargetUserNotFound(Guid staffProfileId) =>
    new(AssignAreaManagerOutcome.TargetUserNotFound,
      $"User '{staffProfileId}' was not found in your company.");

  public static AssignAreaManagerResult TargetNotAreaManager(StaffProfile target) =>
    new(AssignAreaManagerOutcome.TargetNotAreaManager,
      $"User '{target.StaffProfileId}' holds the '{target.Role}' role, " +
      $"not '{Roles.AreaManager}'. " +
      "Only Area Managers can be assigned to a province.");

  public static AssignAreaManagerResult TargetInactive(StaffProfile target) =>
    new(AssignAreaManagerOutcome.TargetInactive,
      $"User '{target.StaffProfileId}' is inactive and cannot be " +
      "assigned to a province.");

  public static AssignAreaManagerResult TargetAlreadyManagesAnotherProvince(
    StaffProfile target,
    Guid otherProvinceId) =>
    new(AssignAreaManagerOutcome.TargetAlreadyManagesAnotherProvince,
      $"User '{target.StaffProfileId}' is already the active Area Manager " +
      $"of province '{otherProvinceId}'. Assigning them to multiple " +
      "provinces requires an explicit decision and is not supported by " +
      "this endpoint.");
}