using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Application.Hierarchy;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/hierarchy")]
public sealed class HierarchyController : ControllerBase
{
  private readonly IHierarchyReadService _hierarchy;
  private readonly IHierarchyRollUpService _rollUp;

  public HierarchyController(
    IHierarchyReadService hierarchy,
    IHierarchyRollUpService rollUp)
  {
    _hierarchy = hierarchy;
    _rollUp = rollUp;
  }

  [HttpGet]
  [Authorize(Policy = RolePolicies.RequireHierarchyReader)]
  [ProducesResponseType(
    typeof(HierarchyTreeResponse),
    StatusCodes.Status200OK)]
  [ProducesResponseType(StatusCodes.Status404NotFound)]
  public async Task<ActionResult<HierarchyTreeResponse>> Get(
    CancellationToken cancellationToken)
  {
    var hierarchy = await _hierarchy.GetHierarchyAsync(
      cancellationToken);

    if (hierarchy is null)
    {
      return NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Company hierarchy not found",
        Detail =
          "No active company hierarchy exists for the authenticated tenant."
      });
    }

    return Ok(hierarchy);
  }

  /// <summary>
  /// Returns one Company Admin summary row per province, including active
  /// hierarchy counts and coverage gaps. This endpoint is read-only.
  /// </summary>
  [HttpGet("roll-up")]
  [Authorize(Policy = RolePolicies.RequireCompanyAdmin)]
  [ProducesResponseType(
    typeof(IReadOnlyList<ProvinceRollUpResponse>),
    StatusCodes.Status200OK)]
  public async Task<ActionResult<IReadOnlyList<ProvinceRollUpResponse>>> RollUp(
    CancellationToken cancellationToken)
  {
    return Ok(await _rollUp.ListAsync(cancellationToken));
  }
}
