using Sellora.CoreService.Application.Common;

namespace Sellora.CoreService.Application.Territories;

/// <summary>
/// Read-side queries over territories. Same scoping guarantee as
/// IAgencyReadService — the caller's province scope is enforced from the
/// JWT, not trusted from a request field.
/// </summary>
public interface ITerritoryReadService
{
  Task<PagedResponse<TerritoryResponse>> ListAsync(
    TerritoryListQuery query,
    CancellationToken cancellationToken = default);
}