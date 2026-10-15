using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sellora.CoreService.Api.Tenancy;

namespace Sellora.CoreService.UnitTests.Api.Tenancy;

/// <summary>
/// HttpTenantContext.CompanyId (_sut): reads the companyId claim of the
/// current request and returns it as a Guid, or null when there is no
/// usable claim, so tenant-filtered queries return no rows.
/// </summary>
/// <remarks>
/// <para>
/// Characteristics and blocks (disjoint and complete per characteristic):
/// C1 HttpContext: present (base) / null.
/// C2 companyId claim: a valid GUID (base) / absent / not a GUID ("acme") /
/// the empty string.
/// C2 is only defined when C1 is present, so the null-context row carries
/// no claim.
/// </para>
/// <para>
/// Boundary values: none numeric. Guid.Empty is not a row: the code accepts
/// it as a valid company ID, which does not match the class comment; that
/// is raised as a draft issue instead of being locked in by a test.
/// </para>
/// <para>
/// Coverage criterion: Base Choice, 1 + (2-1) + (4-1) = 5 rows.
/// TheoryData is used because the expected value is a nullable Guid, which
/// InlineData cannot hold.
/// </para>
/// <para>
/// Test double: IHttpContextAccessor is a Moq stub. It only supplies the
/// HttpContext as input; the test checks the returned value, never the
/// calls made to the accessor.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class HttpTenantContextTests
{
  private static readonly Guid CompanyId =
    Guid.Parse("3f2b8c1e-5a4d-4e6f-9b1a-2c3d4e5f6a7b");

  private readonly Mock<IHttpContextAccessor> _accessor = new();
  private readonly HttpTenantContext _sut;

  public HttpTenantContextTests()
  {
    _sut = new HttpTenantContext(_accessor.Object);
  }

  public static TheoryData<bool, string?, Guid?> CompanyIdCases => new()
  {
    { true, "3f2b8c1e-5a4d-4e6f-9b1a-2c3d4e5f6a7b", CompanyId }, // Base: valid GUID claim
    { true, null, null }, // C2 claim absent
    { true, "acme", null }, // C2 claim not a GUID
    { true, "", null }, // C2 claim is the empty string
    { false, null, null } // C1 no HttpContext
  };

  [Theory]
  [MemberData(nameof(CompanyIdCases))]
  public void CompanyId_ContextAndClaim_ReturnsParsedGuidOrNull(
    bool hasHttpContext,
    string? companyIdClaim,
    Guid? expected)
  {
    // Arrange
    _accessor
      .Setup(accessor => accessor.HttpContext)
      .Returns(CreateHttpContext(hasHttpContext, companyIdClaim));

    // Act
    var result = _sut.CompanyId;

    // Assert
    result.Should().Be(expected);
  }

  private static HttpContext? CreateHttpContext(
    bool hasHttpContext,
    string? companyIdClaim)
  {
    var claims = companyIdClaim is null
      ? Array.Empty<Claim>()
      : new[] { new Claim("companyId", companyIdClaim) };

    return hasHttpContext
      ? new DefaultHttpContext
        {
          User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"))
        }
      : null;
  }
}
