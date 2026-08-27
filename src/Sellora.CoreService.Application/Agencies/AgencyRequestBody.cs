namespace Sellora.CoreService.Api.Contracts;

/// <summary>
/// JSON body of POST /api/agencies. Carries the target province and the
/// agency's own details. The caller's company is derived from the JWT and
/// province ownership is validated in the service against the caller's live
/// province-manager assignments — never trust province membership from the
/// client alone.
/// </summary>
public sealed class RegisterAgencyRequestBody
{
  public Guid ProvinceId { get; set; }
  public Guid OperatorId { get; set; }
  public string Name { get; set; } = string.Empty;
  public string? Email { get; set; }
  public string? Phone { get; set; }
  public string? Address { get; set; }
}