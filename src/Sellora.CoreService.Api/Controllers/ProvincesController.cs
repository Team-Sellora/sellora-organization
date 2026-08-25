using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Application.ProvinceAssignments;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/provinces")]
public sealed class ProvincesController : ControllerBase
{
  private readonly IProvinceAssignmentService _assignments;

  public ProvincesController(IProvinceAssignmentService assignments)
  {
    _assignments = assignments;
  }

  /// <summary>
  /// Assigns an Area Manager to a province. Restricted to CompanyAdmin.
  /// Reassignment ends the prior active assignment and creates the new one
  /// in a single transaction, keeping exactly one active row per province.
  /// </summary>
  [HttpPut("{provinceId:guid}/area-manager")]
  [Authorize(Policy = RolePolicies.RequireCompanyAdmin)]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  public async Task<IActionResult> AssignAreaManager(
    Guid provinceId,
    [FromBody] AssignAreaManagerRequestBody body,
    CancellationToken cancellationToken)
  {
    if (body is null || body.AreaManagerId == Guid.Empty)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid request body",
        Detail = "areaManagerId is required."
      });
    }

    var request = new AssignAreaManagerRequest(provinceId, body.AreaManagerId);

    var result = await _assignments.AssignAreaManagerAsync(
      request,
      cancellationToken);

    return result.Outcome switch
    {
      AssignAreaManagerOutcome.Success => Ok(new
      {
        assignmentId = result.Assignment!.AssignmentId,
        provinceId = result.Assignment.ProvinceId,
        areaManagerId = result.Assignment.AreaManagerId,
        startsAt = result.Assignment.StartsAt
      }),

      AssignAreaManagerOutcome.ProvinceNotFound => NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Province not found",
        Detail = result.Message
      }),

      AssignAreaManagerOutcome.TargetAlreadyManagesAnotherProvince =>
        Conflict(new ProblemDetails
        {
          Status = StatusCodes.Status409Conflict,
          Title = "Target already manages another province",
          Detail = result.Message
        }),

      _ => BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Assignment rejected",
        Detail = result.Message
      })
    };
  }
}