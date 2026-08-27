using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sellora.CoreService.Application.SalesRepAssignments;

namespace Sellora.CoreService.Api.Controllers;

[ApiController]
[Route("api/rep-shop-relationships")]
public sealed class RepShopRelationshipsController : ControllerBase
{
  private readonly IRepShopRelationshipVerifier _verifier;

  public RepShopRelationshipsController(
    IRepShopRelationshipVerifier verifier)
  {
    _verifier = verifier;
  }

  [HttpGet("verify")]
  [Authorize]
  [ProducesResponseType(
    typeof(VerifyRepShopRelationshipResponse),
    StatusCodes.Status200OK)]
  [ProducesResponseType(
    typeof(ProblemDetails),
    StatusCodes.Status400BadRequest)]
  public async Task<IActionResult> Verify(
    [FromQuery] Guid repId,
    [FromQuery] Guid shopId,
    CancellationToken cancellationToken)
  {
    if (repId == Guid.Empty || shopId == Guid.Empty)
    {
      return BadRequest(new ProblemDetails
      {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid verification request",
        Detail = "repId and shopId are required."
      });
    }

    var result = await _verifier.VerifyAsync(
      repId,
      shopId,
      cancellationToken);

    return Ok(result);
  }
}