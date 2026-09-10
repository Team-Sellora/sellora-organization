using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Application.SalesRepAssignments;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/sales-reps")]
[Authorize(Policy = RolePolicies.RequireAgencyOperator)]
public sealed class SalesRepsController : ControllerBase
{
  private readonly ISalesRepAssignmentReadService _readService;

  public SalesRepsController(ISalesRepAssignmentReadService readService)
  {
    _readService = readService;
  }

  [HttpGet]
  public async Task<IActionResult> List(CancellationToken cancellationToken) =>
    Ok(await _readService.ListAsync(cancellationToken));

  [HttpGet("unassigned-territories")]
  public async Task<IActionResult> ListUnassignedTerritories(
    CancellationToken cancellationToken) =>
    Ok(await _readService.ListUnassignedTerritoriesAsync(cancellationToken));
}