using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

/// <summary>The top-level tenant. Every other entity belongs to a company.</summary>
public class Company : ITenantScoped
{
  public Guid CompanyId { get; set; }
  public string TenantCode { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string Status { get; set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; set; }
  public DateTimeOffset? UpdatedAt { get; set; }
}