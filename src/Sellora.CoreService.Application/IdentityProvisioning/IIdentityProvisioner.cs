namespace Sellora.CoreService.Application.IdentityProvisioning;

/// <summary>A login to create in the identity provider.</summary>
/// <param name="Email">Used as the username and the contact address.</param>
/// <param name="Role">One of the Sellora role names (Domain.Identity.Roles).</param>
/// <param name="CompanyId">Always the creating admin's own company, from their token.</param>
public sealed record NewIdentityUser(
  string Email,
  string DisplayName,
  string? Phone,
  string Role,
  Guid CompanyId);

/// <param name="IdentityId">
/// The identity provider's user ID. WSO2 IS 7 puts this same value in the
/// access token's <c>sub</c>, so it is stored as <c>identity_sub</c>.
/// </param>
/// <param name="TemporaryPassword">
/// Set only when the provider was not asked to email an invitation. Shown
/// to the admin once and never stored.
/// </param>
public sealed record ProvisionedIdentity(
  string IdentityId,
  string UserName,
  string? TemporaryPassword);

/// <summary>
/// Creates and removes logins in the identity provider, so users are
/// created from the Sellora app instead of the identity provider's console.
/// </summary>
public interface IIdentityProvisioner
{
  /// <exception cref="IdentityUserAlreadyExistsException">The username is taken.</exception>
  /// <exception cref="IdentityProvisioningUnavailableException">Not configured or the provider failed.</exception>
  Task<ProvisionedIdentity> CreateUserAsync(
    NewIdentityUser user,
    CancellationToken cancellationToken);

  /// <summary>Best-effort removal, used to undo a half-finished creation.</summary>
  Task DeleteUserAsync(string identityId, CancellationToken cancellationToken);
}

public sealed class IdentityUserAlreadyExistsException(string userName)
  : Exception($"A login for '{userName}' already exists in the identity provider.")
{
  public string UserName { get; } = userName;
}

public sealed class IdentityProvisioningUnavailableException(string message, Exception? inner = null)
  : Exception(message, inner);
