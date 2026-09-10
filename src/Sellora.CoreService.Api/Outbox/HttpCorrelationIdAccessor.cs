using Sellora.CoreService.Api.Middleware;
using Sellora.CoreService.Application.Outbox;

namespace Sellora.CoreService.Api.Outbox;

public sealed class HttpCorrelationIdAccessor : ICorrelationIdAccessor
{
  private readonly IHttpContextAccessor _httpContextAccessor;

  public HttpCorrelationIdAccessor(IHttpContextAccessor httpContextAccessor)
  {
    _httpContextAccessor = httpContextAccessor;
  }

  public string GetCorrelationId()
  {
    var context = _httpContextAccessor.HttpContext;

    if (context?.Items.TryGetValue(
          CorrelationIdMiddleware.ItemKey,
          out var value) == true &&
        value is string correlationId &&
        !string.IsNullOrWhiteSpace(correlationId))
    {
      return correlationId;
    }

    // Background/system-created events have no HTTP request.
    return Guid.NewGuid().ToString();
  }
}