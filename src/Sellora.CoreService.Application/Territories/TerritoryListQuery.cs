namespace Sellora.CoreService.Application.Territories;

/// <summary>
/// Normalised, already-validated inputs to the territory list operation.
/// See AgencyListQuery for the split rationale.
/// </summary>
public sealed record TerritoryListQuery(
  string Status,
  Guid? ProvinceId,
  int Page,
  int PageSize);