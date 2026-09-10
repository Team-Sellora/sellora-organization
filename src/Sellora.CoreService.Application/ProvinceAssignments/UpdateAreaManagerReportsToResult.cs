using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;

namespace Sellora.CoreService.Application.ProvinceAssignments;

public enum UpdateAreaManagerReportsToOutcome
{
  Success,
  ActiveAssignmentNotFound,
  ReportsToAdminNotFound,
  ReportsToUserNotCompanyAdmin,
  ReportsToUserInactive
}

public sealed class UpdateAreaManagerReportsToResult
{
  public UpdateAreaManagerReportsToOutcome Outcome { get; }
  public string Message { get; }
  public ProvinceManagerAssignment? Assignment { get; }

  private UpdateAreaManagerReportsToResult(
    UpdateAreaManagerReportsToOutcome outcome,
    string message,
    ProvinceManagerAssignment? assignment = null)
  {
    Outcome = outcome;
    Message = message;
    Assignment = assignment;
  }

  public static UpdateAreaManagerReportsToResult Success(
    ProvinceManagerAssignment assignment) =>
    new(
      UpdateAreaManagerReportsToOutcome.Success,
      "Area Manager reporting line updated.",
      assignment);

  public static UpdateAreaManagerReportsToResult ActiveAssignmentNotFound(
    Guid provinceId) =>
    new(
      UpdateAreaManagerReportsToOutcome.ActiveAssignmentNotFound,
      $"Province '{provinceId}' has no active Area Manager assignment.");

  public static UpdateAreaManagerReportsToResult ReportsToAdminNotFound(
    Guid staffProfileId) =>
    new(
      UpdateAreaManagerReportsToOutcome.ReportsToAdminNotFound,
      $"CompanyAdmin '{staffProfileId}' was not found in your company.");

  public static UpdateAreaManagerReportsToResult
    ReportsToUserNotCompanyAdmin(StaffProfile target) =>
    new(
      UpdateAreaManagerReportsToOutcome.ReportsToUserNotCompanyAdmin,
      $"User '{target.StaffProfileId}' holds the '{target.Role}' role, " +
      $"not '{Roles.CompanyAdmin}'.");

  public static UpdateAreaManagerReportsToResult
    ReportsToUserInactive(StaffProfile target) =>
    new(
      UpdateAreaManagerReportsToOutcome.ReportsToUserInactive,
      $"CompanyAdmin '{target.StaffProfileId}' is inactive.");
}
