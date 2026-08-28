using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

internal static class OutboxEventAssertions
{
  public static async Task AssertEventAsync(
    CoreDbContext db,
    string eventType,
    Guid companyId,
    Guid aggregateId,
    params (string Name, Guid Value)[] expectedPayloadIds)
  {
    var messages = await db.OutboxMessages
      .IgnoreQueryFilters()
      .Where(item =>
        item.EventType == eventType &&
        item.CompanyId == companyId &&
        item.AggregateId == aggregateId)
      .ToListAsync();

    var message = messages
      .OrderByDescending(item => item.OccurredAt)
      .FirstOrDefault();

    Assert.NotNull(message);
    Assert.Equal("1.0", message.SchemaVersion);

    using var document = JsonDocument.Parse(message.Payload);
    var payload = document.RootElement;

    Assert.Equal(eventType, payload.GetProperty("eventType").GetString());
    Assert.Equal("1.0", payload.GetProperty("schemaVersion").GetString());
    Assert.Equal(companyId, payload.GetProperty("companyId").GetGuid());
    Assert.Equal(aggregateId, payload.GetProperty("entityId").GetGuid());
    Assert.Equal(
      message.CorrelationId,
      payload.GetProperty("correlationId").GetString());

    foreach (var (name, value) in expectedPayloadIds)
    {
      Assert.Equal(value, payload.GetProperty(name).GetGuid());
    }
  }
}