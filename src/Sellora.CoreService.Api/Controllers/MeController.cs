using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Api.Authorization;
using Sellora.CoreService.Application.Me;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize(Policy = RolePolicies.RequireHierarchyReader)]
public sealed class MeController : ControllerBase
{
  private readonly ICallerScopeService _scope;

  public MeController(ICallerScopeService scope)
  {
    _scope = scope;
  }

  /// <summary>
  /// The caller's place in the hierarchy, resolved from the token's sub.
  /// Order and Inventory call this (forwarding the user's token) instead
  /// of reading salesRepId / agencyId claims.
  /// </summary>
  [HttpGet("scope")]
  [ProducesResponseType(typeof(CallerScopeResponse), StatusCodes.Status200OK)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
  [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
  public async Task<IActionResult> GetScope(CancellationToken cancellationToken)
  {
    var result = await _scope.GetAsync(cancellationToken);

    return result.Outcome switch
    {
      CallerScopeOutcome.Found => Ok(result.Scope),
      CallerScopeOutcome.NotAuthenticated => Unauthorized(new ProblemDetails
      {
        Status = StatusCodes.Status401Unauthorized,
        Title = "Incomplete identity",
        Detail = result.Message
      }),
      _ => NotFound(new ProblemDetails
      {
        Status = StatusCodes.Status404NotFound,
        Title = "Profile not found",
        Detail = result.Message
      })
    };
  }
}
