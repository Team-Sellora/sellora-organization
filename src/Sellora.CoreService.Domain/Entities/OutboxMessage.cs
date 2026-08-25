using Sellora.CoreService.Domain.Tenancy;

namespace Sellora.CoreService.Domain.Entities;

public class OutboxMessage : ITenantScoped
{
  public Guid OutboxId { get; set; }
  public Guid CompanyId { get; set; }
  public string AggregateType { get; set; } = string.Empty;
  public Guid AggregateId { get; set; }
  public string EventType { get; set; } = string.Empty;

  // Mapped to PostgreSQL jsonb through Fluent configuration.
  public string Payload { get; set; } = string.Empty;

  public Guid CorrelationId { get; set; }
  public DateTimeOffset OccurredAt { get; set; }
  public DateTimeOffset? PublishedAt { get; set; }
}