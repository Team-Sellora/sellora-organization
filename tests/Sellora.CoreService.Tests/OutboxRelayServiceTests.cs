using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Outbox;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Relay bookkeeping — leasing, marking published, scheduling retries — with
/// an in-process publisher against real PostgreSQL (the relay's bulk updates
/// are not translatable on SQLite). OutboxRelayKafkaIntegrationTests covers
/// the real Kafka round trip.
///
/// The relay deliberately ignores tenant filters, so it sees every pending
/// row in the shared fixture database. Rows each test leaves behind are
/// either published or scheduled far in the future, so they never affect
/// the next test.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OutboxRelayServiceTests
  : IClassFixture<PostgreSqlConstraintFixture>, IAsyncLifetime
{
  private readonly PostgreSqlConstraintFixture _fixture;
  private readonly Guid _companyId = Guid.NewGuid();
  private readonly ServiceProvider _services;

  public OutboxRelayServiceTests(PostgreSqlConstraintFixture fixture)
  {
    _fixture = fixture;
    _services = new ServiceCollection()
      .AddScoped<CoreDbContext>(_ => _fixture.CreateDbContext())
      .BuildServiceProvider();
  }

  public async Task InitializeAsync()
  {
    await using var db = _fixture.CreateDbContext();
    db.Companies.Add(new Company
    {
      CompanyId = _companyId,
      TenantCode = $"RELAY-{_companyId:N}",
      Name = "Relay Test Company",
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    });
    await db.SaveChangesAsync();
  }

  public async Task DisposeAsync() => await _services.DisposeAsync();

  [Fact]
  public async Task Process_PublishesDueMessagesInOrder_AndMarksThemPublished()
  {
    var first = await AddMessageAsync(occurredAt: DateTimeOffset.UtcNow.AddMinutes(-2));
    var second = await AddMessageAsync(occurredAt: DateTimeOffset.UtcNow.AddMinutes(-1));
    var notYetDue = await AddMessageAsync(nextAttemptAt: DateTimeOffset.UtcNow.AddDays(1));
    var leasedElsewhere = await AddMessageAsync(leaseExpiresAt: DateTimeOffset.UtcNow.AddDays(1));

    var publisher = new RecordingPublisher();

    await Relay(publisher).ProcessPendingMessagesAsync();

    publisher.Published.Select(message => message.OutboxId)
      .Should().Equal(first, second);
    publisher.Published[0].CorrelationId.Should().Be("relay-test");
    publisher.Published[0].CompanyId.Should().Be(_companyId);

    var stored = await LoadAsync();
    stored[first].PublishedAt.Should().NotBeNull();
    stored[first].LeaseId.Should().BeNull();
    stored[first].LeaseExpiresAt.Should().BeNull();
    stored[second].PublishedAt.Should().NotBeNull();
    stored[notYetDue].PublishedAt.Should().BeNull();
    stored[leasedElsewhere].PublishedAt.Should().BeNull();
  }

  [Fact]
  public async Task Process_RespectsBatchSize()
  {
    for (var i = 0; i < 3; i++)
    {
      await AddMessageAsync(occurredAt: DateTimeOffset.UtcNow.AddMinutes(-10 + i));
    }

    var publisher = new RecordingPublisher();

    await Relay(publisher, new OutboxRelayOptions { BatchSize = 2 }).ProcessPendingMessagesAsync();
    publisher.Published.Should().HaveCount(2);

    // Drain the remainder so later tests start from a clean queue.
    await Relay(publisher).ProcessPendingMessagesAsync();
    publisher.Published.Should().HaveCount(3);
  }

  [Fact]
  public async Task Process_WhenPublishFails_RecordsAttemptAndSchedulesRetry()
  {
    var id = await AddMessageAsync();
    var longError = new string('x', 2500);

    await Relay(new FailingPublisher(longError), new OutboxRelayOptions { RetryDelaySeconds = 3600 })
      .ProcessPendingMessagesAsync();

    var stored = (await LoadAsync())[id];
    stored.PublishedAt.Should().BeNull();
    stored.AttemptCount.Should().Be(1);
    stored.LastError.Should().HaveLength(2000, "errors are truncated to fit the column");
    stored.NextAttemptAt.Should().BeAfter(DateTimeOffset.UtcNow.AddMinutes(30));
    stored.LeaseId.Should().BeNull();
    stored.LeaseExpiresAt.Should().BeNull();
  }

  [Fact]
  public async Task Process_ShortErrorIsStoredAsIs()
  {
    var id = await AddMessageAsync();

    await Relay(new FailingPublisher("broker down"), new OutboxRelayOptions { RetryDelaySeconds = 3600 })
      .ProcessPendingMessagesAsync();

    (await LoadAsync())[id].LastError.Should().Be("broker down");
  }

  [Fact]
  public async Task BackgroundLoop_PublishesUntilStopped()
  {
    var id = await AddMessageAsync();
    var publisher = new RecordingPublisher();
    var relay = Relay(publisher, new OutboxRelayOptions { PollingIntervalSeconds = 1 });

    await relay.StartAsync(CancellationToken.None);

    // Wait for the row to be marked published, not just for the publish
    // call: stopping in between would cancel the bookkeeping update.
    var deadline = DateTime.UtcNow.AddSeconds(15);
    while ((await LoadAsync())[id].PublishedAt is null &&
           DateTime.UtcNow < deadline)
    {
      await Task.Delay(100);
    }

    await relay.StopAsync(CancellationToken.None);

    publisher.Published.Should().Contain(message => message.OutboxId == id);
    (await LoadAsync())[id].PublishedAt.Should().NotBeNull();
  }

  [Fact]
  public async Task BackgroundLoop_SurvivesAPollingFailure()
  {
    // A scope that cannot build a DbContext makes every poll throw.
    await using var broken = new ServiceCollection()
      .AddScoped<CoreDbContext>(_ => throw new InvalidOperationException("db unavailable"))
      .BuildServiceProvider();

    var relay = new OutboxRelayService(
      broken.GetRequiredService<IServiceScopeFactory>(),
      new RecordingPublisher(),
      Options.Create(new OutboxRelayOptions { PollingIntervalSeconds = 1 }),
      NullLogger<OutboxRelayService>.Instance);

    await relay.StartAsync(CancellationToken.None);
    await Task.Delay(200);

    var stop = () => relay.StopAsync(CancellationToken.None);

    await stop.Should().NotThrowAsync("polling failures are logged, not fatal");
  }

  private OutboxRelayService Relay(IEventPublisher publisher, OutboxRelayOptions? options = null) =>
    new(
      _services.GetRequiredService<IServiceScopeFactory>(),
      publisher,
      Options.Create(options ?? new OutboxRelayOptions()),
      NullLogger<OutboxRelayService>.Instance);

  private async Task<Guid> AddMessageAsync(
    DateTimeOffset? occurredAt = null,
    DateTimeOffset? nextAttemptAt = null,
    DateTimeOffset? leaseExpiresAt = null)
  {
    var now = DateTimeOffset.UtcNow;
    var message = new OutboxMessage
    {
      OutboxId = Guid.NewGuid(),
      CompanyId = _companyId,
      AggregateType = "Agency",
      AggregateId = Guid.NewGuid(),
      EventType = "AgencyRegistered",
      Payload = "{}",
      CorrelationId = "relay-test",
      OccurredAt = occurredAt ?? now.AddSeconds(-5),
      NextAttemptAt = nextAttemptAt ?? now.AddSeconds(-5),
      LeaseId = leaseExpiresAt is null ? null : Guid.NewGuid(),
      LeaseExpiresAt = leaseExpiresAt
    };

    await using var db = _fixture.CreateDbContext();
    db.OutboxMessages.Add(message);
    await db.SaveChangesAsync();

    return message.OutboxId;
  }

  private async Task<Dictionary<Guid, OutboxMessage>> LoadAsync()
  {
    await using var db = _fixture.CreateDbContext();

    return await db.OutboxMessages
      .IgnoreQueryFilters()
      .AsNoTracking()
      .Where(message => message.CompanyId == _companyId)
      .ToDictionaryAsync(message => message.OutboxId);
  }

  private sealed class RecordingPublisher : IEventPublisher
  {
    private readonly List<OutboxMessageToPublish> _published = new();

    public IReadOnlyList<OutboxMessageToPublish> Published
    {
      get
      {
        lock (_published)
        {
          return _published.ToList();
        }
      }
    }

    public Task PublishAsync(OutboxMessageToPublish message, CancellationToken cancellationToken = default)
    {
      lock (_published)
      {
        _published.Add(message);
      }

      return Task.CompletedTask;
    }
  }

  private sealed class FailingPublisher(string error) : IEventPublisher
  {
    public Task PublishAsync(OutboxMessageToPublish message, CancellationToken cancellationToken = default) =>
      throw new InvalidOperationException(error);
  }
}
