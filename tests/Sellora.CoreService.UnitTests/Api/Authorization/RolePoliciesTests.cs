using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Sellora.CoreService.Api.Authorization;

namespace Sellora.CoreService.UnitTests.Api.Authorization;

/// <summary>
/// The role policies registered by RolePolicies.AddSelloraRolePolicies,
/// evaluated by the real DefaultAuthorizationService (_sut) against a
/// principal carrying one role claim. Sociable unit test: the framework
/// collaborators are real, but there is no host, no HTTP request and no
/// database.
/// </summary>
/// <remarks>
/// <para>
/// Characteristics and blocks (disjoint and complete per characteristic):
/// C1 policy kind: single role, RequireAreaManager (base) / any-of,
/// RequireHierarchyReader / any-of, RequireStaffManager.
/// C2 claim type: the raw "roles" claim, RoleClaimKind.Raw (base) /
/// ClaimTypes.Role, RoleClaimKind.Mapped.
/// C3 role value: in the policy (base) / not in the policy.
/// </para>
/// <para>
/// Boundary values: none numeric. Role name case is not varied: case
/// handling belongs to ClaimsPrincipal.HasClaim, not to this code.
/// </para>
/// <para>
/// Coverage criterion: Base Choice, 1 + (3-1) + (2-1) + (2-1) = 5 rows, plus
/// one extra row (row 6). The Base Choice rows for C1 only change the policy
/// while the role stays in it, so the any-of check (HasAnyRole) would only
/// ever return true. Row 6 is an any-of policy with a role that is not in
/// it, so a fault that makes HasAnyRole always true is caught.
/// </para>
/// <para>
/// Row 3 depends on MapInboundClaims = false in the JWT bearer setup: WSO2
/// tokens arrive with an unmapped "roles" claim, and the policies match only
/// that claim type, so a ClaimTypes.Role claim alone must not pass.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class RolePoliciesTests : IDisposable
{
  public enum RoleClaimKind
  {
    Raw,
    Mapped
  }

  private readonly ServiceProvider _services;
  private readonly IAuthorizationService _sut;

  public RolePoliciesTests()
  {
    _services = new ServiceCollection()
      .AddLogging()
      .AddAuthorization(options => options.AddSelloraRolePolicies())
      .BuildServiceProvider();

    _sut = _services.GetRequiredService<IAuthorizationService>();
  }

  public void Dispose() => _services.Dispose();

  [Theory]
  [InlineData(RolePolicies.RequireAreaManager, RoleClaimKind.Raw, "AreaManager", true)] // Base: single role policy, raw claim, role in policy
  [InlineData(RolePolicies.RequireAreaManager, RoleClaimKind.Raw, "SalesRep", false)] // C3 role not in policy
  [InlineData(RolePolicies.RequireAreaManager, RoleClaimKind.Mapped, "AreaManager", false)] // C2 mapped ClaimTypes.Role claim
  [InlineData(RolePolicies.RequireHierarchyReader, RoleClaimKind.Raw, "ShopOwner", true)] // C1 any-of, RequireHierarchyReader
  [InlineData(RolePolicies.RequireStaffManager, RoleClaimKind.Raw, "AgencyOperator", true)] // C1 any-of, RequireStaffManager
  [InlineData(RolePolicies.RequireStaffManager, RoleClaimKind.Raw, "AreaManager", false)] // Extra: any-of policy, role not in policy
  public async Task AuthorizeAsync_RoleClaimAgainstPolicy_SucceedsOnlyForRawRolesClaimWithPolicyRole(
    string policyName,
    RoleClaimKind claimKind,
    string role,
    bool expected)
  {
    // Arrange
    var user = CreateUser(claimKind, role);

    // Act
    var result = await _sut.AuthorizeAsync(user, policyName);

    // Assert
    result.Succeeded.Should().Be(expected);
  }

  private static ClaimsPrincipal CreateUser(RoleClaimKind claimKind, string role)
  {
    var claimType = claimKind switch
    {
      RoleClaimKind.Raw => "roles",
      RoleClaimKind.Mapped => ClaimTypes.Role,
      _ => throw new ArgumentOutOfRangeException(nameof(claimKind), claimKind, null)
    };

    return new ClaimsPrincipal(
      new ClaimsIdentity(
        new[] { new Claim(claimType, role) },
        authenticationType: "Bearer"));
  }
}
