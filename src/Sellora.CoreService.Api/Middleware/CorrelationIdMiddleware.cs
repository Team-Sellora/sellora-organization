using Serilog.Context;

namespace Sellora.CoreService.Api.Middleware;

/// <summary>
/// Assigns a correlation ID to each request — reused from the incoming
/// X-Correlation-ID header if present, otherwise generated — and pushes
/// it into the Serilog log context so every log line for the request carries it.
/// The ID is also returned on the response so callers can reference it.
/// </summary>
public class CorrelationIdMiddleware
{
  public const string ItemKey = "Sellora.CorrelationId";
  private const string HeaderName = "X-Correlation-ID";
  private readonly RequestDelegate _next;

  public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

  public async Task InvokeAsync(HttpContext context)
  {
    var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
        && !string.IsNullOrWhiteSpace(existing)
        ? existing.ToString()
        : Guid.NewGuid().ToString();

    // Return it on the response so clients/other services can correlate.
    context.Response.Headers[HeaderName] = correlationId;
    context.Items[ItemKey] = correlationId;
    // Push into the log context: every log written during this request
    // will automatically include CorrelationId.
    using (LogContext.PushProperty("CorrelationId", correlationId))
    {
      await _next(context);
    }
  }
}