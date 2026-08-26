using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Application.AreaManagers;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/area-managers")]
public sealed class AreaManagersController : ControllerBase
{
  private readonly IAreaManagerReadService _service;

  public AreaManagersController(IAreaManagerReadService service)
  {
    _service = service;
  }

  /// <summary>
  /// Lists active Area Managers in the caller's company. Feeds the
  /// "assign manager" dropdown on the province list screen so admins
  /// can never accidentally select a user without the right role.
  /// </summary>
  [HttpGet]
  [Authorize(Policy = RolePolicies.RequireCompanyAdmin)]
  [ProducesResponseType(
    typeof(IReadOnlyList<AreaManagerSummary>),
    StatusCodes.Status200OK)]
  public async Task<ActionResult<IReadOnlyList<AreaManagerSummary>>> List(
    CancellationToken cancellationToken)
  {
    return Ok(await _service.ListActiveAsync(cancellationToken));
  }
}