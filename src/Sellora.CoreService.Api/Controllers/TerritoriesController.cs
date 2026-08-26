using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Application.Common;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Application.TerritoryAssignments;
namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/territories")]
public sealed class TerritoriesController : ControllerBase
{
  private readonly ITerritoryRegistrationService _registrationService;
  private readonly ITerritoryReadService _readService;
  private readonly ITerritoryAgencyAssignmentService _assignmentService;

  public TerritoriesController(
    ITerritoryRegistrationService registrationService,
    ITerritoryReadService readService,
    ITerritoryAgencyAssignmentService assignmentService)
  {
    _registrationService = registrationService;
    _readService = readService;
    _assignmentService = assignmentService;
  }

  /// <summary>
  /// Lists territories scoped to the caller's currently-managed provinces.
  /// Silent scoping: rows in provinces the caller does not manage are
  /// simply absent from the response.
  /// </summary>
  [HttpGet]
  [Authorize(Policy = RolePolicies.RequireAreaManager)]
  [ProducesResponseType(
    typeof(PagedResponse<TerritoryResponse>),
    StatusCodes.Status200OK)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> List(
    [FromQuery] TerritoryListQueryParams parameters,
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
      new TerritoryListQuery(
        normalised.Status,
        normalised.ProvinceId,
        normalised.Page,
        normalised.PageSize,
        normalised.Assigned),
      cancellationToken);

    return Ok(page);
  }

  /// <summary>
  /// Creates a new territory within a province the caller manages.
  /// Territory codes must be unique across the entire company.
  /// </summary>
  [HttpPost]
  [Authorize(Policy = RolePolicies.RequireAreaManager)]
  [ProducesResponseType(
    typeof(TerritoryResponse),
    StatusCodes.Status201Created)]
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
    [FromBody] RegisterTerritoryRequestBody body,
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

    var request = new RegisterTerritoryRequest(
      body.ProvinceId,
      body.Code,
      body.Name,
      body.GeographicDescription);

    var result = await _registrationService.RegisterAsync(
      request,
      cancellationToken);

    return result.Outcome switch
    {
      RegisterTerritoryOutcome.Success => Created(
        $"/api/territories/{result.Territory!.TerritoryId}",
        new TerritoryResponse(
          result.Territory.TerritoryId,
          result.Territory.ProvinceId,
          result.Territory.Code,
          result.Territory.Name,
          result.Territory.GeographicDescription,
          result.Territory.Status,
          result.Territory.CreatedAt)),

      RegisterTerritoryOutcome.CallerNotAnActiveAreaManager or
      RegisterTerritoryOutcome.ProvinceNotManagedByCaller =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
          Status = StatusCodes.Status403Forbidden,
          Title = "Territory creation outside your scope",
          Detail = result.Message
        }),

      RegisterTerritoryOutcome.ProvinceNotFound => NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Province not found",
        Detail = result.Message
      }),

      RegisterTerritoryOutcome.DuplicateTerritoryCode => Conflict(
        new ProblemDetails
        {
          Status = StatusCodes.Status409Conflict,
          Title = "Duplicate territory code",
          Detail = result.Message
        }),

      RegisterTerritoryOutcome.DuplicateTerritoryName => Conflict(
        new ProblemDetails
        {
          Status = StatusCodes.Status409Conflict,
          Title = "Duplicate territory name",
          Detail = result.Message
        }),

      _ => BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Territory creation rejected",
        Detail = result.Message
      })
    };
  }

  [HttpPut("{territoryId:guid}/agency")]
  [Authorize(Policy = RolePolicies.RequireAreaManager)]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  [ProducesResponseType(
  typeof(ProblemDetails),
  StatusCodes.Status400BadRequest)]
  [ProducesResponseType(
  typeof(ProblemDetails),
  StatusCodes.Status409Conflict)]
  public async Task<IActionResult> AssignAgency(
    Guid territoryId,
    [FromBody] AssignTerritoryAgencyRequestBody body,
    CancellationToken cancellationToken)
  {
    if (body is null || body.AgencyId == Guid.Empty)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid assignment request",
        Detail = "agencyId is required."
      });
    }

    var result = await _assignmentService.AssignAsync(
      new AssignTerritoryAgencyRequest(territoryId, body.AgencyId),
      cancellationToken);

    return result.Outcome switch
    {
      AssignTerritoryAgencyOutcome.Success => Ok(new
      {
        result.Assignment!.AssignmentId,
        result.Assignment.TerritoryId,
        result.Assignment.AgencyId,
        result.Assignment.StartsAt
      }),

      AssignTerritoryAgencyOutcome.CallerNotAnActiveAreaManager or
      AssignTerritoryAgencyOutcome.TerritoryNotInManagedProvinces or
      AssignTerritoryAgencyOutcome.AgencyNotInManagedProvinces or
      AssignTerritoryAgencyOutcome.AgencyNotInTerritoryProvince =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
          Status = StatusCodes.Status403Forbidden,
          Title = "Territory assignment outside your scope",
          Detail = result.Message
        }),

      AssignTerritoryAgencyOutcome.OpenWorkBlocksReassignment =>
        Conflict(new ProblemDetails
        {
          Status = StatusCodes.Status409Conflict,
          Title = "Territory reassignment blocked by open work",
          Detail = result.Message,
          Extensions =
          {
            ["blockingReferences"] = result.BlockingReferences
          }
        }),

      AssignTerritoryAgencyOutcome.TerritoryNotFound or
      AssignTerritoryAgencyOutcome.AgencyNotFound =>
        NotFound(new ProblemDetails
        {
          Status = StatusCodes.Status404NotFound,
          Title = "Assignment target not found",
          Detail = result.Message
        }),

      _ => BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Territory assignment rejected",
        Detail = result.Message
      })
    };
  }

  /// <summary>
  /// Normalises and validates the raw query-string parameters. Same shape
  /// as AgenciesController's helper; kept local so each endpoint owns its
  /// own defaults instead of coupling through a shared base controller.
  /// </summary>
  private static bool TryNormaliseListQuery(
    TerritoryListQueryParams parameters,
    out (
      string Status,
      Guid? ProvinceId,
      int Page,
      int PageSize,
      bool? Assigned
    ) normalised,
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

    normalised = (
      status,
      parameters.ProvinceId,
      page,
      pageSize,
      parameters.Assigned
    );
    return true;
  }
}