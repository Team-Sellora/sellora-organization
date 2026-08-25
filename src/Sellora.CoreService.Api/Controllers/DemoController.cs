using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;

namespace Sellora.CoreService.Api.Controllers;

/// <summary>
/// Demonstration endpoints — one per role — used to verify the authentication
/// and authorization pipeline end to end before real business logic exists.
/// Each endpoint is protected by the corresponding role policy. This controller
/// is scaffolding and is expected to be removed or replaced per service.
/// </summary>
[ApiController]
[Route("api/demo")]
public class DemoController : ControllerBase
{
  /// <summary>Accessible only to Company Admins.</summary>
  [HttpGet("company-admin")]
  [Authorize(Policy = RolePolicies.RequireCompanyAdmin)]
  public IActionResult CompanyAdminOnly() =>
      Ok(new { role = "CompanyAdmin", message = "Access granted for Company Admin." });

  /// <summary>Accessible only to Area Managers.</summary>
  [HttpGet("area-manager")]
  [Authorize(Policy = RolePolicies.RequireAreaManager)]
  public IActionResult AreaManagerOnly() =>
      Ok(new { role = "AreaManager", message = "Access granted for Area Manager." });

  /// <summary>Accessible only to Agency Operators.</summary>
  [HttpGet("agency-operator")]
  [Authorize(Policy = RolePolicies.RequireAgencyOperator)]
  public IActionResult AgencyOperatorOnly() =>
      Ok(new { role = "AgencyOperator", message = "Access granted for Agency Operator." });

  /// <summary>Accessible only to Sales Reps.</summary>
  [HttpGet("sales-rep")]
  [Authorize(Policy = RolePolicies.RequireSalesRep)]
  public IActionResult SalesRepOnly() =>
      Ok(new { role = "SalesRep", message = "Access granted for Sales Rep." });

  /// <summary>Accessible only to Shop Owners.</summary>
  [HttpGet("shop-owner")]
  [Authorize(Policy = RolePolicies.RequireShopOwner)]
  public IActionResult ShopOwnerOnly() =>
      Ok(new { role = "ShopOwner", message = "Access granted for Shop Owner." });
}