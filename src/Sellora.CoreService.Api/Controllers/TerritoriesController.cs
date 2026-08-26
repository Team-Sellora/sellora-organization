using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Application.Territories;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/territories")]
public sealed class TerritoriesController : ControllerBase
{
  private readonly ITerritoryRegistrationService _registrationService;

  public TerritoriesController(
    ITerritoryRegistrationService registrationService)
  {
    _registrationService = registrationService;
  }

  /// <summary>
  /// Creates a new territory within a province the caller manages.
  ///
  /// Restricted to AreaManager by policy; province ownership is validated
  /// inside the service against the caller's current province-manager
  /// assignments. Territory codes must be unique across the entire company
  /// — a duplicate returns 409 naming the conflicting code.
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

      // Both 403 paths share the same status. The Message names which one
      // fired so the React screen (CSP-72) can surface it inline.
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
}