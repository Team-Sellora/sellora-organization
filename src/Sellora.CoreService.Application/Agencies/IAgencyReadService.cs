using Sellora.CoreService.Application.Common;

namespace Sellora.CoreService.Application.Agencies;

/// <summary>
/// Read-side queries over agencies. Scoping to the caller's managed
/// provinces is enforced *inside* the implementation from the JWT — the
/// caller cannot escape their scope by sending a province ID that belongs
/// to another Area Manager.
/// </summary>
public interface IAgencyReadService
{
  Task<PagedResponse<AgencyResponse>> ListAsync(
    AgencyListQuery query,
    CancellationToken cancellationToken = default);
}