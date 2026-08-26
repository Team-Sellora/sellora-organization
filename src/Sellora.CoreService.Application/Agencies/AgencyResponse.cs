namespace Sellora.CoreService.Application.Agencies;

/// <summary>
/// Response shape for a single agency. Reused by POST /api/agencies (CSP-67)
/// and — once CSP-70 lands — the list endpoint, so both surfaces share one
/// contract and drift is impossible.
/// </summary>
public sealed record AgencyResponse(
  Guid AgencyId,
  Guid ProvinceId,
  string Name,
  string? Email,
  string? Phone,
  string? Address,
  string Status,
  DateTimeOffset CreatedAt);