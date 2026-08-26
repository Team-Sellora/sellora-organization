namespace Sellora.CoreService.Api.Contracts;

/// <summary>
/// Query-string binding for GET /api/agencies. All fields nullable so the
/// controller can apply centralised defaults from PagingLimits and produce
/// a single validation pass — no drift between what MVC binds and what the
/// service receives.
/// </summary>
public sealed class AgencyListQueryParams
{
  public string? Status { get; set; }
  public Guid? ProvinceId { get; set; }
  public int? Page { get; set; }
  public int? PageSize { get; set; }
}