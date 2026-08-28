namespace Sellora.CoreService.Application.Outbox;

public sealed record NewOutboxMessage(
  Guid CompanyId,
  string AggregateType,
  Guid AggregateId,
  string EventType,
  string SchemaVersion,
  string Payload,
  Guid CorrelationId,
  DateTimeOffset OccurredAt);

public interface IOutboxWriter
{
  void Enqueue(NewOutboxMessage message);
}

public sealed record OutboxMessageToPublish(
  Guid OutboxId,
  string EventType,
  string SchemaVersion,
  Guid CompanyId,
  Guid AggregateId,
  string Payload,
  Guid CorrelationId,
  DateTimeOffset OccurredAt);

public interface IEventPublisher
{
  Task PublishAsync(
    OutboxMessageToPublish message,
    CancellationToken cancellationToken = default);
}