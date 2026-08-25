using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Api.Tenancy;

/// <summary>
/// Reads and validates the company UUID from the authenticated JWT.
/// An absent or invalid claim returns null so tenant-filtered queries
/// return no rows instead of accidentally exposing data.
/// </summary>
public class HttpTenantContext : ITenantContext
{
  private readonly IHttpContextAccessor _accessor;

  public HttpTenantContext(IHttpContextAccessor accessor)
  {
    _accessor = accessor;
  }

  public Guid? CompanyId
  {
    get
    {
      var claimValue =
        _accessor.HttpContext?.User.FindFirst("companyId")?.Value;

      return Guid.TryParse(claimValue, out var companyId)
        ? companyId
        : null;
    }
  }
}