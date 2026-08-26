using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Application.Agencies;

/// <summary>Named outcomes of a register-agency attempt.</summary>
public enum RegisterAgencyOutcome
{
  /// <summary>Agency created. Maps to 201.</summary>
  Success,

  /// <summary>
  /// The JWT subject does not resolve to an active AreaManager staff profile
  /// in the caller's company. The role policy is a coarse gate; this is the
  /// data-level check the policy alone cannot perform. Maps to 403.
  /// </summary>
  CallerNotAnActiveAreaManager,

  /// <summary>
  /// Province not found in the caller's company. The global tenant filter
  /// zeroes out rows from other companies, so this is indistinguishable from
  /// "does not exist" — that is the desired behaviour. Maps to 404.
  /// </summary>
  ProvinceNotFound,

  /// <summary>
  /// Province exists in the caller's company, but the caller is not the
  /// current active manager of it. This is the "outside my province" path
  /// from AC2. Maps to 403.
  /// </summary>
  ProvinceNotManagedByCaller,

  /// <summary>
  /// Insert failed the (province_id, name) unique index. CSP-69 will refine
  /// the surfaced message; this outcome exists so CSP-67 does not leak a
  /// raw 500 when the constraint fires. Maps to 409.
  /// </summary>
  DuplicateAgencyName,

  /// <summary>Body-level validation failed (e.g. blank name). Maps to 400.</summary>
  InvalidRequest
}

/// <summary>
/// Result of a register-agency attempt. Carries a stable Outcome for control
/// flow and a human-readable Message that names the specific problem, so the
/// React screen can surface a clear inline error instead of a generic failure.
/// </summary>
public sealed class RegisterAgencyResult
{
  public RegisterAgencyOutcome Outcome { get; }
  public string Message { get; }
  public Agency? Agency { get; }

  private RegisterAgencyResult(
    RegisterAgencyOutcome outcome,
    string message,
    Agency? agency = null)
  {
    Outcome = outcome;
    Message = message;
    Agency = agency;
  }

  public bool IsSuccess => Outcome == RegisterAgencyOutcome.Success;

  public static RegisterAgencyResult Success(Agency agency) =>
    new(RegisterAgencyOutcome.Success, "Agency registered.", agency);

  public static RegisterAgencyResult CallerNotAnActiveAreaManager() =>
    new(RegisterAgencyOutcome.CallerNotAnActiveAreaManager,
      "The caller does not resolve to an active Area Manager in this " +
      "company. Agency registration is restricted to active Area Managers.");

  public static RegisterAgencyResult ProvinceNotFound(Guid provinceId) =>
    new(RegisterAgencyOutcome.ProvinceNotFound,
      $"Province '{provinceId}' was not found in your company.");

  public static RegisterAgencyResult ProvinceNotManagedByCaller(
    Guid provinceId) =>
    new(RegisterAgencyOutcome.ProvinceNotManagedByCaller,
      $"You are not the active Area Manager of province '{provinceId}'. " +
      "Agency registration is restricted to the manager responsible for " +
      "the target province.");

  public static RegisterAgencyResult DuplicateAgencyName(
    string name,
    Guid provinceId) =>
    new(RegisterAgencyOutcome.DuplicateAgencyName,
      $"An agency named '{name}' already exists in province " +
      $"'{provinceId}'. Agency names must be unique within a province.");

  public static RegisterAgencyResult InvalidRequest(string message) =>
    new(RegisterAgencyOutcome.InvalidRequest, message);
}