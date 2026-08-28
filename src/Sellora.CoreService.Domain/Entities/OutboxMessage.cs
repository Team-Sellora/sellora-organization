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

  public int AttemptCount { get; set; }

  public string? LastError { get; set; }

  public DateTimeOffset NextAttemptAt { get; set; }

  public Guid? LeaseId { get; set; }

  public DateTimeOffset? LeaseExpiresAt { get; set; }

  public string SchemaVersion { get; set; } = "1.0";
}