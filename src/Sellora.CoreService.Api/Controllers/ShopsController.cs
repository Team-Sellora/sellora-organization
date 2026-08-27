using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Api.Contracts;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Application.Shops;
using Sellora.CoreService.Domain.Entities;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/shops")]
public sealed class ShopsController : ControllerBase
{

  private const decimal SriLankaMinimumLatitude = 5.9m;
  private const decimal SriLankaMaximumLatitude = 9.9m;
  private const decimal SriLankaMinimumLongitude = 79.4m;
  private const decimal SriLankaMaximumLongitude = 81.9m;
  private readonly IShopRegistrationService _registrationService;
  private readonly IShopUpdateService _updateService;
  private readonly IShopReadService _readService;

  public ShopsController(
    IShopRegistrationService registrationService,
    IShopUpdateService updateService,
    IShopReadService readService)
  {
    _registrationService = registrationService;
    _updateService = updateService;
    _readService = readService;
  }

  [HttpGet]
  [Authorize(Policy = RolePolicies.RequireAgencyOperator)]
  [ProducesResponseType(
    typeof(PagedResponse<ShopResponse>),
    StatusCodes.Status200OK)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> List(
    [FromQuery] ShopListQueryParams parameters,
    CancellationToken cancellationToken)
  {
    var status = string.IsNullOrWhiteSpace(parameters.Status)
      ? HierarchyStatus.Active
      : parameters.Status.Trim();

    if (status != HierarchyStatus.Active &&
        status != HierarchyStatus.Inactive)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid status filter",
        Detail = "status must be 'Active' or 'Inactive'."
      });
    }

    var page = parameters.Page ?? 1;

    if (page < 1)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid page",
        Detail = "page must be greater than or equal to 1."
      });
    }

    var pageSize = parameters.PageSize ?? 25;

    if (pageSize < 1 || pageSize > 100)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid pageSize",
        Detail = "pageSize must be between 1 and 100."
      });
    }

    var pageResult = await _readService.ListAsync(
      new ShopListQuery(
        status,
        parameters.TerritoryId,
        page,
        pageSize),
      cancellationToken);

    return Ok(pageResult);
  }

  [HttpPost]
  [Authorize(Policy = RolePolicies.RequireAgencyOperator)]
  [ProducesResponseType(StatusCodes.Status201Created)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
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

    if (body.Latitude is null)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Missing shop location",
        Detail = "latitude is required. GPS coordinates are mandatory for shop registration."
      });
    }

    if (body.Longitude is null)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Missing shop location",
        Detail = "longitude is required. GPS coordinates are mandatory for shop registration."
      });
    }

    if (body.Latitude < SriLankaMinimumLatitude ||
        body.Latitude > SriLankaMaximumLatitude)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid latitude",
        Detail =
          $"latitude must be between {SriLankaMinimumLatitude} and " +
          $"{SriLankaMaximumLatitude}, within Sri Lanka."
      });
    }

    if (body.Longitude < SriLankaMinimumLongitude ||
        body.Longitude > SriLankaMaximumLongitude)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid longitude",
        Detail =
          $"longitude must be between {SriLankaMinimumLongitude} and " +
          $"{SriLankaMaximumLongitude}, within Sri Lanka."
      });
    }

    if (body.CreditLimit is null)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Missing credit limit",
        Detail = "creditLimit is required."
      });
    }

    if (body.CreditLimit <= 0)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid credit limit",
        Detail = "creditLimit must be greater than zero."
      });
    }

    var result = await _registrationService.RegisterAsync(
      new RegisterShopRequest(
        body.TerritoryId,
        body.Name,
        body.OwnerName,
        body.OwnerIdentitySub,
        body.OwnerEmail,
        body.OwnerPhone,
        body.Address,
        body.Latitude.Value,
        body.Longitude.Value,
        body.CreditLimit.Value),
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

      RegisterShopOutcome.OwnerIdentitySubRequired => BadRequest(
        new ProblemDetails
        {
          Status = StatusCodes.Status400BadRequest,
          Title = "Missing shop owner identity",
          Detail = result.Message
        }),

      RegisterShopOutcome.OwnerIdentityAlreadyLinked => Conflict(
        new ProblemDetails
        {
          Status = StatusCodes.Status409Conflict,
          Title = "Shop Owner identity already linked",
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

  [HttpPut("{shopId:guid}")]
  [Authorize(Policy = RolePolicies.RequireAgencyOperator)]
  [ProducesResponseType(StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
  public async Task<IActionResult> Update(
    Guid shopId,
    [FromBody] UpdateShopRequestBody body,
    CancellationToken cancellationToken)
  {
    if (body is null ||
        body.Latitude is null ||
        body.Longitude is null ||
        body.CreditLimit is null)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Missing shop update values",
        Detail = "latitude, longitude, and creditLimit are required."
      });
    }

    if (body.Latitude < SriLankaMinimumLatitude ||
        body.Latitude > SriLankaMaximumLatitude ||
        body.Longitude < SriLankaMinimumLongitude ||
        body.Longitude > SriLankaMaximumLongitude)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid shop location",
        Detail = "Coordinates must be within Sri Lanka."
      });
    }

    if (body.CreditLimit <= 0)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid credit limit",
        Detail = "creditLimit must be greater than zero."
      });
    }

    var result = await _updateService.UpdateAsync(
      new UpdateShopRequest(
        shopId,
        body.Latitude.Value,
        body.Longitude.Value,
        body.CreditLimit.Value),
      cancellationToken);

    return result.Outcome switch
    {
      UpdateShopOutcome.Success => Ok(new
      {
        result.Shop!.ShopId,
        result.Shop.Latitude,
        result.Shop.Longitude,
        result.Shop.CreditLimit,
        result.Shop.UpdatedAt
      }),

      UpdateShopOutcome.CallerNotAnActiveAgencyOperator or
      UpdateShopOutcome.ShopOutsideCallerAgency =>
        StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
        {
          Status = StatusCodes.Status403Forbidden,
          Title = "Shop update outside your scope",
          Detail = result.Message
        }),

      UpdateShopOutcome.ShopNotFound => NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Shop not found",
        Detail = result.Message
      }),

      _ => BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Shop update rejected",
        Detail = result.Message
      })
    };
  }
}