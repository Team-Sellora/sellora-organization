namespace Sellora.CoreService.Api.Contracts;

/// <summary>
/// JSON body of POST /api/territories. Carries the target province and the
/// territory's identifying details. The caller's company is derived from the
/// JWT and province ownership is validated in the service against the
/// caller's live province-manager assignments — never trust province
/// membership from the client alone.
/// </summary>
public sealed class RegisterTerritoryRequestBody
{
  public Guid ProvinceId { get; set; }
  public string Code { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string? GeographicDescription { get; set; }
}