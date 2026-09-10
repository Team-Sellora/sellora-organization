namespace Sellora.CoreService.Application.Provinces;

/// <summary>
/// One row of GET /api/provinces. Carries the province identity, the current
/// active Area Manager (or null if none), and active agency/shop counts —
/// the minimum a dashboard needs to render a province card.
/// </summary>
public sealed record ProvinceSummaryResponse(
  Guid ProvinceId,
  string Code,
  string Name,
  string Status,
  DateTimeOffset CreatedAt,
  CurrentManagerSummary? CurrentManager,
  int AgencyCount,
  int ShopCount);

/// <summary>
/// Minimal identity + display info for the province's active Area Manager.
/// Only the fields a dashboard card needs — the full profile lives elsewhere.
/// </summary>
public sealed record CurrentManagerSummary(
  Guid StaffProfileId,
  string DisplayName,
  string? Email);