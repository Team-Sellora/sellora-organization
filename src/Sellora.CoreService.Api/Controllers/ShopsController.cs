using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Application.Shops;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/shops")]
public sealed class ShopsController : ControllerBase
{
  private readonly IShopRegistrationService _registrationService;

  public ShopsController(IShopRegistrationService registrationService)
  {
    _registrationService = registrationService;
  }

  [HttpPost]
  [Authorize(Policy = RolePolicies.RequireAgencyOperator)]
  [ProducesResponseType(StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  public async Task<IActionResult> Register(
    [FromBody] RegisterShopRequestBody body,
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

    if (body.TerritoryId == Guid.Empty)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid territory",
        Detail = "territoryId is required."
      });
    }

    if (string.IsNullOrWhiteSpace(body.Name))
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid shop name",
        Detail = "name is required."
      });
    }

    if (string.IsNullOrWhiteSpace(body.Address))
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid shop address",
        Detail = "address is required."
      });
    }

    var result = await _registrationService.RegisterAsync(
      new RegisterShopRequest(
        body.TerritoryId,
        body.Name,
        body.OwnerName,
        body.OwnerEmail,
        body.OwnerPhone,
        body.Address,
        body.Latitude,
        body.Longitude,
        body.CreditLimit),
      cancellationToken);

    return result.Outcome switch
    {
      RegisterShopOutcome.Success => Created(
        $"/api/shops/{result.Shop!.ShopId}",
        new
        {
          result.Shop.ShopId,
          result.Shop.TerritoryId,
          result.Shop.Name,
          result.Shop.Status,
          result.Shop.CreatedAt
        }),

      RegisterShopOutcome.CallerNotAnActiveAgencyOperator =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
          Status = StatusCodes.Status403Forbidden,
          Title = "Shop registration outside your scope",
          Detail = result.Message
        }),

      RegisterShopOutcome.TerritoryNotAssignedToCallerAgency =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
          Status = StatusCodes.Status403Forbidden,
          Title = "Territory is not assigned to your agency",
          Detail = result.Message
        }),

      RegisterShopOutcome.TerritoryNotFound => NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Territory not found",
        Detail = result.Message
      }),

      _ => BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Shop registration rejected",
        Detail = result.Message
      })
    };
  }
}