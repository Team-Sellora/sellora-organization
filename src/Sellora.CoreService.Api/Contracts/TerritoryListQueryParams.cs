namespace Sellora.CoreService.Api.Contracts;

/// <summary>
/// Query-string binding for GET /api/territories. Same shape as
/// AgencyListQueryParams — deliberately not a shared base class so each
/// endpoint's filters can diverge later without a base-class refactor.
/// </summary>
public sealed class TerritoryListQueryParams
{
  public string? Status { get; set; }
  public Guid? ProvinceId { get; set; }
  public int? Page { get; set; }
  public int? PageSize { get; set; }
  public bool? Assigned { get; set; }
}