using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class SalesRepTerritoryAssignment : ITenantScoped
{
  public Guid AssignmentId { get; set; }
  public Guid CompanyId { get; set; }
  public Guid TerritoryId { get; set; }
  public Guid SalesRepId { get; set; }
  public DateTimeOffset StartsAt { get; set; }
  public DateTimeOffset? EndsAt { get; set; }
  public string CreatedBy { get; set; } = string.Empty;
}