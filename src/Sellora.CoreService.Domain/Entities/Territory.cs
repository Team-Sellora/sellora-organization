using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class Territory : ITenantScoped
{
  public Guid TerritoryId { get; set; }
  public Guid CompanyId { get; set; }
  public Guid ProvinceId { get; set; }
  public string Code { get; set; } = string.Empty;
  public string Name { get; set; } = string.Empty;
  public string? GeographicDescription { get; set; }
  public string Status { get; set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; set; }
}