using System.Security.Claims;
using Sellora.CoreService.Application.Identity;

namespace Sellora.CoreService.Api.Identity;

public sealed class HttpCurrentUserContext : ICurrentUserContext
{
  private readonly IHttpContextAccessor _accessor;

  public HttpCurrentUserContext(
    IHttpContextAccessor accessor)
  {
    _accessor = accessor;
  }

  private ClaimsPrincipal? User =>
    _accessor.HttpContext?.User;

  public string? Subject =>
    User?.FindFirst("sub")?.Value ??
    User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

  public string? Role =>
    User?.FindFirst(ClaimTypes.Role)?.Value ??
    User?.FindFirst("roles")?.Value ??
    User?.FindFirst("role")?.Value;
}