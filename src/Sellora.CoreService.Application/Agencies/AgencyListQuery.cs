namespace Sellora.CoreService.Application.Agencies;

/// <summary>
/// Normalised, already-validated inputs to the agency list operation. The
/// controller does the "user typed a bad pageSize" translation; the service
/// receives values it can trust and use directly.
/// </summary>
public sealed record AgencyListQuery(
  string Status,
  Guid? ProvinceId,
  int Page,
  int PageSize);