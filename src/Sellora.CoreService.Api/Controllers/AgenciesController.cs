using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Application.Hierarchy;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/agencies")]
public sealed class AgenciesController : ControllerBase
{
  private readonly IHierarchyDeactivationService
    _deactivationService;

  public AgenciesController(
    IHierarchyDeactivationService deactivationService)
  {
    _deactivationService = deactivationService;
  }

  /// <summary>
  /// Deactivates an agency while preserving the agency row, territories,
  /// shops, assignments, orders, payments, and audit references.
  /// </summary>
  [HttpPatch("{agencyId:guid}/deactivate")]
  [Authorize(Policy = RolePolicies.RequireCompanyAdmin)]
  [ProducesResponseType(StatusCodes.Status204NoContent)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Deactivate(
    Guid agencyId,
    CancellationToken cancellationToken)
  {
    var deactivated =
      await _deactivationService.DeactivateAgencyAsync(
        agencyId,
        cancellationToken);

    if (!deactivated)
    {
      return NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Agency not found",
        Detail =
          "The agency does not exist or does not belong to your company."
      });
    }

    return NoContent();
  }

  // DELETE is intentionally not implemented. Hierarchy entities are
  // permanent records and must be deactivated to preserve historical
  // references held by orders, payments, audits, and assignments.
}