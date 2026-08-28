using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Outbox;

public sealed class EntityFrameworkOutboxWriter : IOutboxWriter
{
  private readonly CoreDbContext _db;

  public EntityFrameworkOutboxWriter(CoreDbContext db)
  {
    _db = db;
  }

  public void Enqueue(NewOutboxMessage message)
  {
    _db.OutboxMessages.Add(new OutboxMessage
    {
      OutboxId = Guid.NewGuid(),
      CompanyId = message.CompanyId,
      AggregateType = message.AggregateType,
      AggregateId = message.AggregateId,
      EventType = message.EventType,
      SchemaVersion = message.SchemaVersion,
      Payload = message.Payload,
      CorrelationId = message.CorrelationId,
      OccurredAt = message.OccurredAt,
      NextAttemptAt = message.OccurredAt,
      AttemptCount = 0
    });
  }
}