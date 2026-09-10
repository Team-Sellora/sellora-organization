using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Application.Territories;

/// <summary>Named outcomes of a register-territory attempt.</summary>
public enum RegisterTerritoryOutcome
{
  /// <summary>Territory created. Maps to 201.</summary>
  Success,

  /// <summary>
  /// The JWT subject does not resolve to an active AreaManager staff profile
  /// in the caller's company. Maps to 403.
  /// </summary>
  CallerNotAnActiveAreaManager,

  /// <summary>Province not found in the caller's company. Maps to 404.</summary>
  ProvinceNotFound,

  /// <summary>
  /// Province exists in the caller's company, but the caller is not the
  /// current active manager of it. Maps to 403.
  /// </summary>
  ProvinceNotManagedByCaller,

  /// <summary>
  /// Insert failed the (company_id, code) unique index. This is the primary
  /// error CSP-68 exists to surface cleanly — territory codes must be unique
  /// across the entire company, not merely within a province, because they
  /// are referenced by other services and a duplicate would create ambiguity
  /// that is very hard to untangle later. Maps to 409.
  /// </summary>
  DuplicateTerritoryCode,

  /// <summary>
  /// Insert failed the (province_id, name) unique index. A separate outcome
  /// from DuplicateTerritoryCode so the error message names the actual
  /// conflict rather than misleading the caller. Maps to 409.
  /// </summary>
  DuplicateTerritoryName,

  /// <summary>Body-level validation failed (e.g. blank code). Maps to 400.</summary>
  InvalidRequest
}

/// <summary>
/// Result of a register-territory attempt. Carries a stable Outcome for
/// control flow and a human-readable Message that names the specific problem
/// — critically, for the duplicate case, the conflicting code — so the React
/// screen can surface a clear inline error instead of a generic failure.
/// </summary>
public sealed class RegisterTerritoryResult
{
  public RegisterTerritoryOutcome Outcome { get; }
  public string Message { get; }
  public Territory? Territory { get; }

  private RegisterTerritoryResult(
    RegisterTerritoryOutcome outcome,
    string message,
    Territory? territory = null)
  {
    Outcome = outcome;
    Message = message;
    Territory = territory;
  }

  public bool IsSuccess => Outcome == RegisterTerritoryOutcome.Success;

  public static RegisterTerritoryResult Success(Territory territory) =>
    new(RegisterTerritoryOutcome.Success, "Territory created.", territory);

  public static RegisterTerritoryResult CallerNotAnActiveAreaManager() =>
    new(RegisterTerritoryOutcome.CallerNotAnActiveAreaManager,
      "The caller does not resolve to an active Area Manager in this " +
      "company. Territory creation is restricted to active Area Managers.");

  public static RegisterTerritoryResult ProvinceNotFound(Guid provinceId) =>
    new(RegisterTerritoryOutcome.ProvinceNotFound,
      $"Province '{provinceId}' was not found in your company.");

  public static RegisterTerritoryResult ProvinceNotManagedByCaller(
    Guid provinceId) =>
    new(RegisterTerritoryOutcome.ProvinceNotManagedByCaller,
      $"You are not the active Area Manager of province '{provinceId}'. " +
      "Territory creation is restricted to the manager responsible for " +
      "the target province.");

  public static RegisterTerritoryResult DuplicateTerritoryCode(string code) =>
    new(RegisterTerritoryOutcome.DuplicateTerritoryCode,
      $"Territory code '{code}' is already in use in this company. " +
      "Territory codes must be unique across the entire company because " +
      "they are referenced by other services.");

  public static RegisterTerritoryResult DuplicateTerritoryName(
    string name,
    Guid provinceId) =>
    new(RegisterTerritoryOutcome.DuplicateTerritoryName,
      $"A territory named '{name}' already exists in province " +
      $"'{provinceId}'. Territory names must be unique within a province.");

  public static RegisterTerritoryResult InvalidRequest(string message) =>
    new(RegisterTerritoryOutcome.InvalidRequest, message);
}