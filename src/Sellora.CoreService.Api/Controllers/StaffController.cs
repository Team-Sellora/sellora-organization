using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Application.Staff;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/staff")]
[Authorize(Policy = RolePolicies.RequireStaffManager)]
public sealed class StaffController : ControllerBase
{
  private readonly IStaffProvisioningService _provisioning;

  public StaffController(IStaffProvisioningService provisioning)
  {
    _provisioning = provisioning;
  }

  /// <summary>
  /// Adds a staff member: creates their WSO2 login and their staff profile
  /// in one call. The response carries a temporary password once.
  /// </summary>
  [HttpPost]
  [ProducesResponseType(typeof(CreatedStaffResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
  public async Task<IActionResult> Create(
    [FromBody] CreateStaffRequest body,
    CancellationToken cancellationToken)
  {
    var result = await _provisioning.CreateAsync(body, cancellationToken);

    if (result.Outcome == CreateStaffOutcome.Created)
    {
      // Never cache a response that contains a password.
      Response.Headers.CacheControl = "no-store";
      return Created($"/api/staff/{result.Staff!.StaffProfileId}", result.Staff);
    }

    var (status, title) = result.Outcome switch
    {
      CreateStaffOutcome.InvalidRequest => (StatusCodes.Status400BadRequest, "Invalid staff member"),
      CreateStaffOutcome.NotAllowed => (StatusCodes.Status403Forbidden, "Not allowed to add this role"),
      CreateStaffOutcome.EmailAlreadyUsed => (StatusCodes.Status409Conflict, "Email already in use"),
      _ => (StatusCodes.Status503ServiceUnavailable, "Identity provider unavailable")
    };

    return StatusCode(status, new ProblemDetails { Status = status, Title = title, Detail = result.Message });
  }
}
