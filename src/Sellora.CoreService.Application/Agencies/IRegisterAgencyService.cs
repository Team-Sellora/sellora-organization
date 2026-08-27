namespace Sellora.CoreService.Application.Agencies;

/// <summary>
/// Registers agencies within a province. Restriction to the AreaManager role
/// is enforced by the endpoint policy; this service enforces the data-level
/// guard the policy cannot: that the caller is the *active* Area Manager of
/// the target province at the moment of the request, not merely someone with
/// the AreaManager role somewhere in the company.
/// </summary>
public interface IAgencyRegistrationService
{
  Task<RegisterAgencyResult> RegisterAsync(
    RegisterAgencyRequest request,
    CancellationToken cancellationToken = default);
}