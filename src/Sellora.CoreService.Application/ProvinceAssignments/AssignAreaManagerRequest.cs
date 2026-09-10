namespace Sellora.CoreService.Application.ProvinceAssignments;

/// <summary>
/// Input to the assign-area-manager operation.
///
/// Carries only the province (from the route) and the target user (from the
/// request body). The caller's company is derived from the JWT via
/// ITenantContext and is never trusted from the request, so no companyId
/// field appears here.
/// </summary>
public sealed record AssignAreaManagerRequest(
  Guid ProvinceId,
  Guid AreaManagerId);