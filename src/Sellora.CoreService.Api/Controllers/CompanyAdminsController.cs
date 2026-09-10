using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Application.CompanyAdmins;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/company-admins")]
public sealed class CompanyAdminsController : ControllerBase
{
  private readonly ICompanyAdminReadService _service;

  public CompanyAdminsController(ICompanyAdminReadService service)
  {
    _service = service;
  }

  [HttpGet]
  [Authorize(Policy = RolePolicies.RequireCompanyAdmin)]
  [ProducesResponseType(
    typeof(IReadOnlyList<CompanyAdminSummary>),
    StatusCodes.Status200OK)]
  public async Task<ActionResult<IReadOnlyList<CompanyAdminSummary>>> List(
    CancellationToken cancellationToken)
  {
    return Ok(await _service.ListActiveAsync(cancellationToken));
  }
}
