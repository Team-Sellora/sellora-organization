using System.Net;
using System.Text;

namespace Sellora.CoreService.Tests;

/// <summary>
/// Answers HTTP calls from a test-supplied function and records each request
/// (with its body) so tests can assert on exactly what was sent.
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
  private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

  public StubHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> respond)
  {
    _respond = respond;
  }

  public List<RecordedRequest> Requests { get; } = new();

  public static HttpResponseMessage Json(HttpStatusCode status, string json) =>
    new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

  protected override async Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
  {
    var body = request.Content is null
      ? null
      : await request.Content.ReadAsStringAsync(cancellationToken);

    Requests.Add(new RecordedRequest(
      request.Method,
      request.RequestUri!.PathAndQuery,
      request.Headers.Authorization?.ToString(),
      body));

    return _respond(request);
  }

  internal sealed record RecordedRequest(
    HttpMethod Method,
    string PathAndQuery,
    string? Authorization,
    string? Body);
}

/// <summary>Hands out clients over fixed handlers, keyed by client name.</summary>
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
  private readonly Dictionary<string, (HttpMessageHandler Handler, Uri? BaseAddress)> _clients = new();

  public StubHttpClientFactory Add(string name, HttpMessageHandler handler, Uri? baseAddress = null)
  {
    _clients[name] = (handler, baseAddress);
    return this;
  }

  public HttpClient CreateClient(string name)
  {
    var (handler, baseAddress) = _clients[name];
    return new HttpClient(handler, disposeHandler: false) { BaseAddress = baseAddress };
  }
}
