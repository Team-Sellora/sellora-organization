namespace Sellora.CoreService.Application.Territories;

/// <summary>
/// Input to the register-territory operation.
///
/// Carries only fields the caller supplies. The acting user's identity and
/// the caller's company are read from the JWT via ICurrentUserContext and
/// ITenantContext, so no callerId or companyId field appears here. The
/// province is client-supplied but is validated against the caller's live
/// province-manager assignments in the service.
/// </summary>
public sealed record RegisterTerritoryRequest(
  Guid ProvinceId,
  string Code,
  string Name,
  string? GeographicDescription);