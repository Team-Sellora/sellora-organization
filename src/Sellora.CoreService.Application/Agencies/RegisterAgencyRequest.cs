namespace Sellora.CoreService.Application.Agencies;

/// <summary>
/// Input to the register-agency operation.
///
/// Carries only fields the caller supplies. The acting user's identity and
/// the caller's company are read from the JWT via ICurrentUserContext and
/// ITenantContext, so no callerId or companyId field appears here. The
/// province is client-supplied but is validated against the caller's live
/// province-manager assignments in the service — a client swapping the ID
/// cannot escape their scope.
/// </summary>
public sealed record RegisterAgencyRequest(
  Guid ProvinceId,
  string Name,
  string? Email,
  string? Phone,
  string? Address);