using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Tenancy;
using Sellora.CoreService.Infrastructure.Outbox;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public sealed class OutboxRelayKafkaIntegrationTests
  : IClassFixture<PostgreSqlConstraintFixture>
{
  private const string KafkaBootstrapServers = "localhost:9092";

  private readonly PostgreSqlConstraintFixture _fixture;

  public OutboxRelayKafkaIntegrationTests(
    PostgreSqlConstraintFixture fixture)
  {
    _fixture = fixture;
  }

  [Fact]
  public async Task FailedShopWrite_RollsBackShopAndOutboxRecord()
  {
    var companyId = Guid.NewGuid();
    var territoryId = Guid.NewGuid();
    var shopId = Guid.NewGuid();

    await using var db = _fixture.CreateDbContext(companyId);

    await SeedCompanyAndTerritoryAsync(db, companyId, territoryId);

    db.Shops.Add(new Shop
    {
      ShopId = shopId,
      CompanyId = companyId,
      TerritoryId = territoryId,
      Name = "Rejected Shop",
      OwnerName = "Test Owner",
      Address = "123 Test Road",
      Latitude = 6.927079m,
      Longitude = 79.861244m,
      CreditLimit = -1m, // Violates ck_shop_credit_limit.
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    });

    var writer = new EntityFrameworkOutboxWriter(
      db,
      new FixedCorrelationIdAccessor("csp-94-rollback-test"));

    writer.Enqueue(new NewOutboxMessage(
      companyId,
      "Shop",
      shopId,
      "ShopRegistered",
      "1.0",
      JsonSerializer.Serialize(new
      {
        eventType = "ShopRegistered",
        shopId,
        territoryId
      }),
      DateTimeOffset.UtcNow));

    await Assert.ThrowsAsync<DbUpdateException>(
      () => db.SaveChangesAsync());

    db.ChangeTracker.Clear();

    Assert.False(await db.Shops
      .IgnoreQueryFilters()
      .AnyAsync(shop => shop.ShopId == shopId));

    Assert.False(await db.OutboxMessages
      .IgnoreQueryFilters()
      .AnyAsync(message => message.AggregateId == shopId));
  }

  [Fact]
  public async Task UnavailableBrokerThenRecovery_PublishesCommittedEventExactlyOnce()
  {
    var companyId = Guid.NewGuid();
    var aggregateId = Guid.NewGuid();
    var outboxId = Guid.NewGuid();
    var topic = $"sellora.hierarchy.csp94.{Guid.NewGuid():N}";

    await CreateTopicAsync(topic);

    await using (var db = _fixture.CreateDbContext(companyId))
    {
      db.Companies.Add(new Company
      {
        CompanyId = companyId,
        TenantCode = $"csp94-{Guid.NewGuid():N}",
        Name = "CSP-94 Test Company",
        Status = "Active",
        CreatedAt = DateTimeOffset.UtcNow
      });

      db.OutboxMessages.Add(new OutboxMessage
      {
        OutboxId = outboxId,
        CompanyId = companyId,
        AggregateType = "Shop",
        AggregateId = aggregateId,
        EventType = "ShopRegistered",
        SchemaVersion = "1.0",
        CorrelationId = "csp-94-outage-test",
        Payload = JsonSerializer.Serialize(new
        {
          eventType = "ShopRegistered",
          schemaVersion = "1.0",
          companyId,
          entityId = aggregateId
        }),
        OccurredAt = DateTimeOffset.UtcNow,
        NextAttemptAt = DateTimeOffset.UtcNow,
        AttemptCount = 0
      });

      await db.SaveChangesAsync();
    }

    using (var unavailablePublisher = new KafkaEventPublisher(
      Options.Create(new KafkaOptions
      {
        BootstrapServers = "localhost:9091",
        HierarchyTopic = topic,
        MessageTimeoutMs = 250
      })))
    using (var services = CreateRelayServiceProvider())
    {
      var relay = CreateRelay(services, unavailablePublisher);

      await relay.ProcessPendingMessagesAsync();
    }

    await using (var db = _fixture.CreateDbContext(companyId))
    {
      var pending = await db.OutboxMessages
        .IgnoreQueryFilters()
        .SingleAsync(message => message.OutboxId == outboxId);

      Assert.Null(pending.PublishedAt);
      Assert.Equal(1, pending.AttemptCount);
      Assert.False(string.IsNullOrWhiteSpace(pending.LastError));

      // Make the retry eligible immediately rather than waiting 15 seconds.
      pending.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(-1);
      await db.SaveChangesAsync();
    }

    using (var availablePublisher = new KafkaEventPublisher(
      Options.Create(new KafkaOptions
      {
        BootstrapServers = KafkaBootstrapServers,
        HierarchyTopic = topic,
        MessageTimeoutMs = 10_000
      })))
    using (var services = CreateRelayServiceProvider())
    {
      var relay = CreateRelay(services, availablePublisher);

      await relay.ProcessPendingMessagesAsync();
    }

    await using (var db = _fixture.CreateDbContext(companyId))
    {
      var published = await db.OutboxMessages
        .IgnoreQueryFilters()
        .SingleAsync(message => message.OutboxId == outboxId);

      Assert.NotNull(published.PublishedAt);
      Assert.Equal(1, published.AttemptCount);
    }

    using var consumer = new ConsumerBuilder<string, string>(
      new ConsumerConfig
      {
        BootstrapServers = KafkaBootstrapServers,
        GroupId = $"csp94-{Guid.NewGuid():N}",
        AutoOffsetReset = AutoOffsetReset.Earliest,
        EnableAutoCommit = false
      }).Build();

    consumer.Subscribe(topic);

    var received = consumer.Consume(TimeSpan.FromSeconds(10));

    Assert.NotNull(received);
    Assert.Equal(aggregateId.ToString(), received.Message.Key);
    Assert.Equal("ShopRegistered", received.Message.Value
      .Contains("ShopRegistered") ? "ShopRegistered" : null);

    var eventIdHeader = received.Message.Headers
      .Single(header => header.Key == "event-id");

    Assert.Equal(outboxId.ToByteArray(), eventIdHeader.GetValueBytes());

    // The unique topic should contain only this one published event.
    Assert.Null(consumer.Consume(TimeSpan.FromSeconds(1)));
  }

  private async Task CreateTopicAsync(string topic)
  {
    using var admin = new AdminClientBuilder(
      new AdminClientConfig
      {
        BootstrapServers = KafkaBootstrapServers
      }).Build();

    await admin.CreateTopicsAsync(
    [
      new TopicSpecification
      {
        Name = topic,
        NumPartitions = 1,
        ReplicationFactor = 1
      }
    ]);
  }

  private ServiceProvider CreateRelayServiceProvider()
  {
    var services = new ServiceCollection();

    services.AddSingleton<ITenantContext>(
      new SystemTenantContext());

    services.AddDbContext<CoreDbContext>(
      options => options.UseNpgsql(_fixture.ConnectionString));

    return services.BuildServiceProvider();
  }

  private static OutboxRelayService CreateRelay(
    IServiceProvider services,
    IEventPublisher publisher)
  {
    return new OutboxRelayService(
      services.GetRequiredService<IServiceScopeFactory>(),
      publisher,
      Options.Create(new OutboxRelayOptions
      {
        BatchSize = 10,
        LeaseSeconds = 30,
        RetryDelaySeconds = 1
      }),
      NullLogger<OutboxRelayService>.Instance);
  }

  private static async Task SeedCompanyAndTerritoryAsync(
    CoreDbContext db,
    Guid companyId,
    Guid territoryId)
  {
    var provinceId = Guid.NewGuid();

    db.Companies.Add(new Company
    {
      CompanyId = companyId,
      TenantCode = $"csp94-{Guid.NewGuid():N}",
      Name = "CSP-94 Rollback Company",
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.Provinces.Add(new Province
    {
      ProvinceId = provinceId,
      CompanyId = companyId,
      Code = $"P{Guid.NewGuid():N}"[..12],
      Name = "CSP-94 Province",
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    });

    db.Territories.Add(new Territory
    {
      TerritoryId = territoryId,
      CompanyId = companyId,
      ProvinceId = provinceId,
      Code = $"T{Guid.NewGuid():N}"[..12],
      Name = "CSP-94 Territory",
      Status = "Active",
      CreatedAt = DateTimeOffset.UtcNow
    });

    await db.SaveChangesAsync();
  }

  private sealed class FixedCorrelationIdAccessor
    : ICorrelationIdAccessor
  {
    private readonly string _correlationId;

    public FixedCorrelationIdAccessor(string correlationId)
    {
      _correlationId = correlationId;
    }

    public string GetCorrelationId() => _correlationId;
  }

  private sealed class SystemTenantContext : ITenantContext
  {
    public Guid? CompanyId => null;
  }
}