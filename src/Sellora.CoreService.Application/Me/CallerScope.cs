namespace Sellora.CoreService.Application.Me;

/// <summary>
/// Where the caller sits in the business, derived from the token's
/// <c>sub</c> by Organization — the single source of truth for this.
/// Other services (Order, Inventory) read it instead of carrying business
/// IDs as token claims, so a reassignment takes effect without touching
/// the identity provider.
/// </summary>
public sealed record CallerScopeResponse(
  string Subject,
  Guid CompanyId,
  string Role,
  Guid? StaffProfileId,
  string? DisplayName,
  Guid? SalesRepId,
  Guid? AgencyId,
  Guid? TerritoryId,
  Guid? ShopId,
  IReadOnlyList<Guid> ProvinceIds,
  IReadOnlyList<Guid> AgencyIds,
  IReadOnlyList<Guid> TerritoryIds,
  IReadOnlyList<Guid> ShopIds);

public enum CallerScopeOutcome
{
  Found,
  NotAuthenticated,
  ProfileNotFound
}

public sealed record CallerScopeResult(
  CallerScopeOutcome Outcome,
  CallerScopeResponse? Scope,
  string? Message)
{
  public static CallerScopeResult Found(CallerScopeResponse scope) =>
    new(CallerScopeOutcome.Found, scope, null);

  public static CallerScopeResult NotAuthenticated() =>
    new(
      CallerScopeOutcome.NotAuthenticated,
      null,
      "The access token has no subject, role or company.");

  public static CallerScopeResult ProfileNotFound(string role) =>
    new(
      CallerScopeOutcome.ProfileNotFound,
      null,
      $"No active {role} profile in this company is linked to your login. " +
      "Ask an administrator to create or reactivate it.");
}

public interface ICallerScopeService
{
  Task<CallerScopeResult> GetAsync(CancellationToken cancellationToken);
}
