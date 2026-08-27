using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class AuditEntry : ITenantScoped
{
  public Guid AuditEntryId { get; set; }
  public Guid CompanyId { get; set; }
  public string EntityType { get; set; } = string.Empty;
  public Guid EntityId { get; set; }
  public string FieldName { get; set; } = string.Empty;
  public string OldValue { get; set; } = string.Empty;
  public string NewValue { get; set; } = string.Empty;
  public string ChangedBy { get; set; } = string.Empty;
  public DateTimeOffset ChangedAt { get; set; }
}