using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class StaffProfile : ITenantScoped
{
  public Guid StaffProfileId { get; set; }
  public Guid CompanyId { get; set; }
  public string IdentitySub { get; set; } = string.Empty;
  public string Role { get; set; } = string.Empty;
  public string DisplayName { get; set; } = string.Empty;
  public string? Email { get; set; }
  public string? Phone { get; set; }
  public string Status { get; set; } = string.Empty;
  public DateTimeOffset CreatedAt { get; set; }
}