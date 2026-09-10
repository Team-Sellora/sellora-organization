namespace Sellora.CoreService.Application.Territories;

/// <summary>
/// Response shape for a single territory. Reused by POST /api/territories
/// (CSP-68) and — once CSP-70 lands — the list endpoint, so both surfaces
/// share one contract and drift is impossible.
/// </summary>
public sealed record TerritoryResponse(
  Guid TerritoryId,
  Guid ProvinceId,
  string Code,
  string Name,
  string? GeographicDescription,
  string Status,
  DateTimeOffset CreatedAt);