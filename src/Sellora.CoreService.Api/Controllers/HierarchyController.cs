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

  public HierarchyController(
    IHierarchyReadService hierarchy)
  {
    _hierarchy = hierarchy;
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
}