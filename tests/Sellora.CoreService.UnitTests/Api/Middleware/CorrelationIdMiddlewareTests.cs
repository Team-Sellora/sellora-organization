using Microsoft.AspNetCore.Http;
using Sellora.CoreService.Api.Middleware;

namespace Sellora.CoreService.UnitTests.Api.Middleware;

/// <summary>
/// CorrelationIdMiddleware.InvokeAsync (_sut): reuses the incoming
/// X-Correlation-ID header or generates a GUID, writes the ID to the
/// response header and to HttpContext.Items, then calls the next delegate.
/// </summary>
/// <remarks>
/// <para>
/// Characteristic and blocks (disjoint and complete): the incoming
/// X-Correlation-ID header. Non-blank (base) / whitespace / absent.
/// </para>
/// <para>
/// Boundary values: none numeric. Whitespace is the edge of "blank": it is
/// present but must be treated like a missing header.
/// </para>
/// <para>
/// Coverage criterion: Each Choice, 3 cases, split by expected behaviour.
/// The non-blank header is reused, so its exact value is asserted (Fact);
/// the whitespace and absent headers produce a new GUID, so those rows
/// assert the shape instead (Theory). Splitting them keeps branching out of
/// the test methods.
/// </para>
/// <para>
/// Test double: the next RequestDelegate is a hand-written spy. It records
/// how many times it ran and which correlation ID was in Items at that
/// moment; the assertions check the recording afterwards. That proves Items
/// is set before next runs. It is a spy rather than a mock because nothing
/// is set up as an expectation in advance. Serilog's LogContext is not
/// asserted, because that would test Serilog.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class CorrelationIdMiddlewareTests
{
  private const string HeaderName = "X-Correlation-ID";

  private readonly CorrelationIdMiddleware _sut;
  private int _nextCallCount;
  private string? _correlationIdSeenByNext;

  public CorrelationIdMiddlewareTests()
  {
    _sut = new CorrelationIdMiddleware(RecordNextCall);
  }

  [Fact]
  public async Task InvokeAsync_HeaderPresent_ReusesIt()
  {
    // Arrange
    var context = CreateHttpContext("abc-123");

    // Act
    await _sut.InvokeAsync(context);

    // Assert
    context.Response.Headers[HeaderName].ToString().Should().Be("abc-123");
    context.Items[CorrelationIdMiddleware.ItemKey].Should().Be("abc-123");
    _correlationIdSeenByNext.Should().Be("abc-123");
    _nextCallCount.Should().Be(1);
  }

  [Theory]
  [InlineData("   ")] // Whitespace header
  [InlineData(null)] // Header absent
  public async Task InvokeAsync_HeaderBlankOrMissing_GeneratesGuid(string? incomingHeader)
  {
    // Arrange
    var context = CreateHttpContext(incomingHeader);

    // Act
    await _sut.InvokeAsync(context);

    // Assert
    var responseHeader = context.Response.Headers[HeaderName].ToString();
    Guid.TryParse(responseHeader, out _).Should().BeTrue();
    responseHeader.Should().NotBe(incomingHeader);
    context.Items[CorrelationIdMiddleware.ItemKey].Should().Be(responseHeader);
    _correlationIdSeenByNext.Should().Be(responseHeader);
    _nextCallCount.Should().Be(1);
  }

  private Task RecordNextCall(HttpContext context)
  {
    _nextCallCount++;
    context.Items.TryGetValue(CorrelationIdMiddleware.ItemKey, out var value);
    _correlationIdSeenByNext = value as string;
    return Task.CompletedTask;
  }

  private static DefaultHttpContext CreateHttpContext(string? incomingHeader)
  {
    var context = new DefaultHttpContext();

    if (incomingHeader is not null)
    {
      context.Request.Headers[HeaderName] = incomingHeader;
    }

    return context;
  }
}
