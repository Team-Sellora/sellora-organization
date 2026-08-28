using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Api.Middleware;
using Sellora.CoreService.Application.Outbox;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Tests;

public sealed class OutboxCorrelationTests
  : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public OutboxCorrelationTests(TestWebAppFactory factory)
  {
    _factory = factory;
  }

  [Fact]
  public async Task Enqueue_StoresTheIncomingRequestCorrelationId()
  {
    const string correlationId = "shop-registration-request-123";
    var aggregateId = Guid.NewGuid();

    using var scope = _factory.Services.CreateScope();

    var httpContextAccessor = scope.ServiceProvider
      .GetRequiredService<IHttpContextAccessor>();

    var httpContext = new DefaultHttpContext();
    httpContext.Items[CorrelationIdMiddleware.ItemKey] = correlationId;
    httpContextAccessor.HttpContext = httpContext;

    var writer = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

    writer.Enqueue(new NewOutboxMessage(
      HierarchyEndpointTestData.CompanyId,
      "Shop",
      aggregateId,
      "ShopRegistered",
      "1.0",
      "{}",
      DateTimeOffset.UtcNow));

    var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
    await db.SaveChangesAsync();

    var message = await db.OutboxMessages
      .IgnoreQueryFilters()
      .SingleAsync(item => item.AggregateId == aggregateId);

    Assert.Equal(correlationId, message.CorrelationId);
  }
}