using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sellora.CoreService.Api.Identity;

namespace Sellora.CoreService.UnitTests.Api.Identity;

/// <summary>
/// HttpCurrentUserContext.Subject (_sut): returns the caller's subject from
/// the "sub" claim, falling back to ClaimTypes.NameIdentifier, or null when
/// there is no request or no subject claim.
/// </summary>
/// <remarks>
/// <para>
/// Characteristics and blocks (disjoint and complete per characteristic):
/// C1 HttpContext: present (base) / null.
/// C2 subject claims present: "sub" only (base) / NameIdentifier only /
/// both / neither.
/// C2 is only defined when C1 is present, so the null-context row carries
/// no claims.
/// </para>
/// <para>
/// Boundary values: none numeric.
/// </para>
/// <para>
/// Coverage criterion: Base Choice, 1 + (2-1) + (4-1) = 5 rows. The
/// NameIdentifier-only row tests a defensive fallback: with
/// MapInboundClaims = false, WSO2 tokens arrive with a raw "sub" claim, but
/// the fallback is real code with a stated purpose.
/// </para>
/// <para>
/// Test double: IHttpContextAccessor is a Moq stub. It only supplies the
/// HttpContext as input; the test checks the returned value, never the
/// calls made to the accessor.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class HttpCurrentUserContextTests
{
  private readonly Mock<IHttpContextAccessor> _accessor = new();
  private readonly HttpCurrentUserContext _sut;

  public HttpCurrentUserContextTests()
  {
    _sut = new HttpCurrentUserContext(_accessor.Object);
  }

  [Theory]
  [InlineData(true, "sub-1", null, "sub-1")] // Base: "sub" only
  [InlineData(true, null, "nid-1", "nid-1")] // C2 NameIdentifier only, fallback
  [InlineData(true, "sub-1", "nid-1", "sub-1")] // C2 both, "sub" wins
  [InlineData(true, null, null, null)] // C2 neither
  [InlineData(false, null, null, null)] // C1 no HttpContext
  public void Subject_ContextAndClaims_ReturnsSubThenNameIdentifierOrNull(
    bool hasHttpContext,
    string? subClaim,
    string? nameIdentifierClaim,
    string? expected)
  {
    // Arrange
    _accessor
      .Setup(accessor => accessor.HttpContext)
      .Returns(CreateHttpContext(hasHttpContext, subClaim, nameIdentifierClaim));

    // Act
    var result = _sut.Subject;

    // Assert
    result.Should().Be(expected);
  }

  private static HttpContext? CreateHttpContext(
    bool hasHttpContext,
    string? subClaim,
    string? nameIdentifierClaim)
  {
    var claims = new List<Claim>();

    if (subClaim is not null)
    {
      claims.Add(new Claim("sub", subClaim));
    }

    if (nameIdentifierClaim is not null)
    {
      claims.Add(new Claim(ClaimTypes.NameIdentifier, nameIdentifierClaim));
    }

    return hasHttpContext
      ? new DefaultHttpContext
        {
          User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
        }
      : null;
  }
}
