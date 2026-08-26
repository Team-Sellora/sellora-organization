using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Common;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/agencies")]
public sealed class AgenciesController : ControllerBase
{
  private readonly IHierarchyDeactivationService _deactivationService;
  private readonly IAgencyRegistrationService _registrationService;
  private readonly IAgencyReadService _readService;

  public AgenciesController(
    IHierarchyDeactivationService deactivationService,
    IAgencyRegistrationService registrationService,
    IAgencyReadService readService)
  {
    _deactivationService = deactivationService;
    _registrationService = registrationService;
    _readService = readService;
  }

  /// <summary>
  /// Lists agencies scoped to the caller's currently-managed provinces.
  ///
  /// Silent scoping: rows in provinces the caller does not manage are
  /// simply absent from the response. A provinceId query filter outside
  /// the caller's scope collapses to an empty page — the API never leaks
  /// whether such a province exists in the company.
  /// </summary>
  [HttpGet]
  [Authorize(Policy = RolePolicies.RequireAreaManager)]
  [ProducesResponseType(
    typeof(PagedResponse<AgencyResponse>),
    StatusCodes.Status200OK)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> List(
    [FromQuery] AgencyListQueryParams parameters,
    CancellationToken cancellationToken)
  {
    if (!TryNormaliseListQuery(
      parameters,
      out var normalised,
      out var badRequest))
    {
      return badRequest!;
    }

    var page = await _readService.ListAsync(
      new AgencyListQuery(
        normalised.Status,
        normalised.ProvinceId,
        normalised.Page,
        normalised.PageSize),
      cancellationToken);

    return Ok(page);
  }

  /// <summary>
  /// Registers a new agency in a province the caller manages.
  /// Province ownership is validated inside the service against the
  /// caller's current province-manager assignments.
  /// </summary>
  [HttpPost]
  [Authorize(Policy = RolePolicies.RequireAreaManager)]
  [ProducesResponseType(typeof(AgencyResponse), StatusCodes.Status201Created)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status400BadRequest)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status403Forbidden)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status404NotFound)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status409Conflict)]
  public async Task<IActionResult> Register(
    [FromBody] RegisterAgencyRequestBody body,
    CancellationToken cancellationToken)
  {
    if (body is null)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid request body",
        Detail = "A request body is required."
      });
    }

    var request = new RegisterAgencyRequest(
      body.ProvinceId,
      body.OperatorId,
      body.Name,
      body.Email,
      body.Phone,
      body.Address);

    var result = await _registrationService.RegisterAsync(
      request,
      cancellationToken);

    return result.Outcome switch
    {
      RegisterAgencyOutcome.Success => Created(
        $"/api/agencies/{result.Agency!.AgencyId}",
        new AgencyResponse(
          result.Agency.AgencyId,
          result.Agency.ProvinceId,
          result.Agency.Name,
          result.Agency.Email,
          result.Agency.Phone,
          result.Agency.Address,
          result.Agency.Status,
          result.Agency.CreatedAt)),

      RegisterAgencyOutcome.CallerNotAnActiveAreaManager or
      RegisterAgencyOutcome.ProvinceNotManagedByCaller =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
          Status = StatusCodes.Status403Forbidden,
          Title = "Registration outside your scope",
          Detail = result.Message
        }),

      RegisterAgencyOutcome.ProvinceNotFound => NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Province not found",
        Detail = result.Message
      }),

      RegisterAgencyOutcome.OperatorNotFound => NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Agency operator not found",
        Detail = result.Message
      }),

      RegisterAgencyOutcome.OperatorNotAnActiveAgencyOperator =>
        BadRequest(new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Invalid agency operator",
          Detail = result.Message
        }),

      RegisterAgencyOutcome.DuplicateAgencyName => Conflict(new ProblemDetails
      {
        Status = StatusCodes.Status409Conflict,
        Title = "Duplicate agency name",
        Detail = result.Message
      }),
      _ => BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Registration rejected",
        Detail = result.Message
      })
    };
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

  /// <summary>
  /// Normalises the raw query-string parameters into typed defaults and
  /// validates them. Kept local to the controller because MVC's default
  /// binding gives us "user typed a string in an int field" errors that
  /// are ugly and unhelpful — this hand-rolled pass produces a clean
  /// ProblemDetails naming the specific parameter that failed.
  /// </summary>
  private static bool TryNormaliseListQuery(
    AgencyListQueryParams parameters,
    out (string Status, Guid? ProvinceId, int Page, int PageSize) normalised,
    out IActionResult? badRequest)
  {
    normalised = default;
    badRequest = null;

    var status = string.IsNullOrWhiteSpace(parameters.Status)
      ? HierarchyStatus.Active
      : parameters.Status.Trim();

    if (status != HierarchyStatus.Active &&
        status != HierarchyStatus.Inactive)
    {
      badRequest = new BadRequestObjectResult(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid status filter",
        Detail =
          $"status must be '{HierarchyStatus.Active}' or " +
          $"'{HierarchyStatus.Inactive}'."
      });
      return false;
    }

    var page = parameters.Page ?? PagingLimits.DefaultPage;
    if (page < 1)
    {
      badRequest = new BadRequestObjectResult(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid page",
        Detail = "page must be greater than or equal to 1."
      });
      return false;
    }

    var pageSize = parameters.PageSize ?? PagingLimits.DefaultPageSize;
    if (pageSize < 1 || pageSize > PagingLimits.MaxPageSize)
    {
      badRequest = new BadRequestObjectResult(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid pageSize",
        Detail =
          $"pageSize must be between 1 and {PagingLimits.MaxPageSize}."
      });
      return false;
    }

    normalised = (status, parameters.ProvinceId, page, pageSize);
    return true;
  }

  // DELETE is intentionally not implemented. Hierarchy entities are
  // permanent records and must be deactivated to preserve historical
  // references held by orders, payments, audits, and assignments.
}