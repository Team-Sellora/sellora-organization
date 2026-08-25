using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class ProvinceManagerAssignment : ITenantScoped
{
  public Guid AssignmentId { get; set; }
  public Guid CompanyId { get; set; }
  public Guid ProvinceId { get; set; }
  public Guid AreaManagerId { get; set; }
  public Guid? ReportsToAdminId { get; set; }
  public DateTimeOffset StartsAt { get; set; }
  public DateTimeOffset? EndsAt { get; set; }
  public string CreatedBy { get; set; } = string.Empty;
}