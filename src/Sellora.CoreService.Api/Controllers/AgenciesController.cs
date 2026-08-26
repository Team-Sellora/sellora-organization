using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Application.Agencies;
using Sellora.CoreService.Application.Hierarchy;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/agencies")]
public sealed class AgenciesController : ControllerBase
{
  private readonly IHierarchyDeactivationService _deactivationService;
  private readonly IAgencyRegistrationService _registrationService;

  public AgenciesController(
    IHierarchyDeactivationService deactivationService,
    IAgencyRegistrationService registrationService)
  {
    _deactivationService = deactivationService;
    _registrationService = registrationService;
  }

  /// <summary>
  /// Registers a new agency in a province the caller manages.
  ///
  /// Restricted to AreaManager by policy; province ownership is validated
  /// inside the service against the caller's *current* province-manager
  /// assignments — the request body's provinceId is a client hint, never
  /// authoritative on its own.
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

      // Both 403 paths share the same status. The Message names which one
      // fired so the React screen (CSP-72) can surface it inline.
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

  // DELETE is intentionally not implemented. Hierarchy entities are
  // permanent records and must be deactivated to preserve historical
  // references held by orders, payments, audits, and assignments.
}