namespace Sellora.CoreService.Application.Hierarchy;

/// <summary>
/// One operational summary row per province for the Company Admin roll-up.
/// Counts describe active hierarchy records only.
/// </summary>
public sealed record ProvinceRollUpResponse(
  Guid ProvinceId,
  string Code,
  string Name,
  string Status,
  AreaManagerRollUpSummary? CurrentManager,
  int AgencyCount,
  int TerritoryCount,
  int ShopCount,
  int UnassignedTerritoryCount,
  bool HasUnassignedTerritories);

public sealed record AreaManagerRollUpSummary(
  Guid StaffProfileId,
  string DisplayName,
  string? Email,
  ReportingAdminRollUpSummary? ReportsToAdmin);

public sealed record ReportingAdminRollUpSummary(
  Guid StaffProfileId,
  string DisplayName,
  string? Email);
