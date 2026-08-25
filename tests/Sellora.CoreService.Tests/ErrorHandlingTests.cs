using System.Net;
using Xunit;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Verifies the global error handler returns a structured ProblemDetails response
/// for unhandled exceptions, rather than an HTML error page or raw stack trace.
/// </summary>
public class ErrorHandlingTests : IClassFixture<TestWebAppFactory>
{
  private readonly TestWebAppFactory _factory;

  public ErrorHandlingTests(TestWebAppFactory factory) => _factory = factory;

  [Fact]
  public async Task UnhandledException_Returns500_WithProblemDetailsJson()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/nonexistent");

    Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

    // Structured error body, not HTML: content type must be problem+json.
    Assert.NotNull(response.Content.Headers.ContentType);
    Assert.Equal(
        "application/problem+json",
        response.Content.Headers.ContentType!.MediaType);

    // And it must not be an HTML error page.
    var body = await response.Content.ReadAsStringAsync();
    Assert.DoesNotContain("<html", body, System.StringComparison.OrdinalIgnoreCase);
  }

  [Fact]
  public async Task HealthEndpoint_ReturnsOk_WithoutAuth()
  {
    var client = _factory.CreateClient();

    var response = await client.GetAsync("/health");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }
}