namespace Sellora.CoreService.Api.Contracts;

/// <summary>
/// JSON body of PUT /api/provinces/{id}/area-manager.
/// Carries only the target user — the province is bound from the route,
/// and the company is derived from the JWT.
/// </summary>
public sealed class AssignAreaManagerRequestBody
{
  public Guid AreaManagerId { get; set; }
}