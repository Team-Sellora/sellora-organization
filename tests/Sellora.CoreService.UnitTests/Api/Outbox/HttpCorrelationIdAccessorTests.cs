using Microsoft.AspNetCore.Http;
using Sellora.CoreService.Api.Middleware;
using Sellora.CoreService.Api.Outbox;

namespace Sellora.CoreService.UnitTests.Api.Outbox;

/// <summary>
/// HttpCorrelationIdAccessor.GetCorrelationId (_sut): returns the
/// correlation ID the middleware stored in HttpContext.Items, or a new GUID
/// when there is no usable value (for example, events raised outside an
/// HTTP request).
/// </summary>
/// <remarks>
/// <para>
/// Characteristic and blocks (disjoint and complete): the state of
/// Items[CorrelationIdMiddleware.ItemKey]. Non-blank string (base) /
/// whitespace string / an object that is not a string / no HttpContext.
/// </para>
/// <para>
/// Boundary values: none numeric. A whitespace string is the edge of
/// "usable": it is a string but must be treated as missing.
/// </para>
/// <para>
/// Coverage criterion: Each Choice, 4 cases, split by expected behaviour.
/// The stored value is returned as is (Fact); the other three blocks
/// produce a new GUID, so they assert the shape instead (Theory).
/// </para>
/// <para>
/// Test double: FakeHttpContextAccessor is a fake. It is a working
/// implementation: the test writes HttpContext through the setter and the
/// SUT reads it back through the getter, as with the real accessor inside
/// one request. Its shortcut (a plain field instead of the real accessor's
/// AsyncLocal holder) would be unsafe in production, where concurrent
/// requests would overwrite each other, but is fine inside one test. A stub
/// would instead return a canned answer set up per call. The real
/// HttpContextAccessor is not used because its ambient AsyncLocal state is
/// not what this test is about; the fake keeps the input explicit and owned
/// by the test instance.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class HttpCorrelationIdAccessorTests
{
  public enum UnusableItemKind
  {
    WhitespaceString,
    NonStringObject,
    NoHttpContext
  }

  private readonly FakeHttpContextAccessor _httpContextAccessor = new();
  private readonly HttpCorrelationIdAccessor _sut;

  public HttpCorrelationIdAccessorTests()
  {
    _sut = new HttpCorrelationIdAccessor(_httpContextAccessor);
  }

  [Fact]
  public void GetCorrelationId_ItemPresent_ReturnsIt()
  {
    // Arrange
    _httpContextAccessor.HttpContext = CreateHttpContextWithItem("corr-1");

    // Act
    var result = _sut.GetCorrelationId();

    // Assert
    result.Should().Be("corr-1");
  }

  [Theory]
  [InlineData(UnusableItemKind.WhitespaceString)] // Whitespace string
  [InlineData(UnusableItemKind.NonStringObject)] // Object that is not a string
  [InlineData(UnusableItemKind.NoHttpContext)] // No HttpContext
  public void GetCorrelationId_NoUsableItem_ReturnsNewGuid(UnusableItemKind itemKind)
  {
    // Arrange
    _httpContextAccessor.HttpContext = CreateHttpContext(itemKind);

    // Act
    var result = _sut.GetCorrelationId();

    // Assert
    Guid.TryParse(result, out _).Should().BeTrue();
  }

  private static HttpContext? CreateHttpContext(UnusableItemKind itemKind)
  {
    return itemKind switch
    {
      UnusableItemKind.WhitespaceString => CreateHttpContextWithItem("  "),
      UnusableItemKind.NonStringObject => CreateHttpContextWithItem(
        Guid.Parse("9d1e2f3a-4b5c-4d6e-8f70-81a2b3c4d5e6")),
      UnusableItemKind.NoHttpContext => null,
      _ => throw new ArgumentOutOfRangeException(nameof(itemKind), itemKind, null)
    };
  }

  private static HttpContext CreateHttpContextWithItem(object item)
  {
    var context = new DefaultHttpContext();
    context.Items[CorrelationIdMiddleware.ItemKey] = item;
    return context;
  }

  private sealed class FakeHttpContextAccessor : IHttpContextAccessor
  {
    public HttpContext? HttpContext { get; set; }
  }
}
