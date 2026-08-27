namespace Sellora.CoreService.Application.Territories;

/// <summary>
/// Creates territories within a province. Restriction to the AreaManager
/// role is enforced by the endpoint policy; this service enforces the
/// data-level guards the policy cannot — that the caller is the *active*
/// Area Manager of the target province, and that the territory code is
/// unique across the entire company.
/// </summary>
public interface ITerritoryRegistrationService
{
  Task<RegisterTerritoryResult> RegisterAsync(
    RegisterTerritoryRequest request,
    CancellationToken cancellationToken = default);
}