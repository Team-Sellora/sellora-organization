using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class Agency : ITenantScoped, ISoftDeactivatable
{
  public Guid AgencyId { get; set; }
  public Guid CompanyId { get; set; }
  public Guid ProvinceId { get; set; }
  public string Name { get; set; } = string.Empty;
  public string? Email { get; set; }
  public string? Phone { get; set; }
  public string? Address { get; set; }
  public string Status { get; set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; set; }
}