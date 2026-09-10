using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class Province : ITenantScoped, ISoftDeactivatable
{
  public Guid ProvinceId { get; set; }
  public Guid CompanyId { get; set; }
  public string Code { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string Status { get; set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; set; }
}