using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Application.ProvinceAssignments;
using Sellora.CoreService.Application.Provinces;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/provinces")]
public sealed class ProvincesController : ControllerBase
{
  private readonly IProvinceAssignmentService _assignments;
  private readonly IProvinceReadService _provinces;

  public ProvincesController(
    IProvinceAssignmentService assignments,
    IProvinceReadService provinces)
  {
    _assignments = assignments;
    _provinces = provinces;
  }

  /// <summary>
  /// Lists provinces in the caller's company, each with its current active
  /// Area Manager (or null) and active agency/shop counts. Backed by a
  /// single aggregate query so it stays cheap under dashboard polling.
  /// </summary>
  [HttpGet]
  [Authorize(Policy = RolePolicies.RequireCompanyAdmin)]
  [ProducesResponseType(
    typeof(IReadOnlyList<ProvinceSummaryResponse>),
    StatusCodes.Status200OK)]
  public async Task<ActionResult<IReadOnlyList<ProvinceSummaryResponse>>> List(
    CancellationToken cancellationToken)
  {
    var provinces = await _provinces.ListAsync(cancellationToken);
    return Ok(provinces);
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
        reportsToAdminId = result.Assignment.ReportsToAdminId,
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

      AssignAreaManagerOutcome.NoActiveCompanyAdmin =>
        BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "No reporting admin available",
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

  /// <summary>
  /// Changes the reporting contact for the province's active Area Manager.
  /// It updates organisational metadata only and does not alter access scope.
  /// </summary>
  [HttpPut("{provinceId:guid}/area-manager/reports-to")]
  [Authorize(Policy = RolePolicies.RequireCompanyAdmin)]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  public async Task<IActionResult> UpdateAreaManagerReportsTo(
    Guid provinceId,
    [FromBody] UpdateAreaManagerReportsToRequestBody body,
    CancellationToken cancellationToken)
  {
    if (body is null || body.ReportsToAdminId == Guid.Empty)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid request body",
        Detail = "reportsToAdminId is required."
      });
    }

    var result = await _assignments.UpdateAreaManagerReportsToAsync(
      new UpdateAreaManagerReportsToRequest(
        provinceId,
        body.ReportsToAdminId),
      cancellationToken);

    return result.Outcome switch
    {
      UpdateAreaManagerReportsToOutcome.Success => Ok(new
      {
        assignmentId = result.Assignment!.AssignmentId,
        provinceId = result.Assignment.ProvinceId,
        areaManagerId = result.Assignment.AreaManagerId,
        reportsToAdminId = result.Assignment.ReportsToAdminId
      }),

      UpdateAreaManagerReportsToOutcome.ActiveAssignmentNotFound =>
        NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Active Area Manager assignment not found",
          Detail = result.Message
        }),

      _ => BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Reporting line update rejected",
        Detail = result.Message
      })
    };
  }
}
