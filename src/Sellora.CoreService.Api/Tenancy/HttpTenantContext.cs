using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Api.Tenancy;

/// <summary>
/// Reads the current company ID from the authenticated user's claims on the
/// active HTTP request.
/// </summary>
public class HttpTenantContext : ITenantContext
{
  private readonly IHttpContextAccessor _accessor;

  public HttpTenantContext(IHttpContextAccessor accessor) => _accessor = accessor;

  public string? CompanyId =>
    _accessor.HttpContext?.User.FindFirst("companyId")?.Value;
}