namespace Sellora.CoreService.Application.Staff;

public sealed record CreateStaffRequest(
  string? Role,
  string? DisplayName,
  string? Email,
  string? Phone);

/// <summary>
/// The created staff member. <see cref="TemporaryPassword"/> is returned
/// once, in this response only, so the admin can hand it over.
/// </summary>
public sealed record CreatedStaffResponse(
  Guid StaffProfileId,
  string Role,
  string DisplayName,
  string Email,
  string? Phone,
  string Status,
  string IdentitySub,
  string UserName,
  string? TemporaryPassword);

public enum CreateStaffOutcome
{
  Created,
  InvalidRequest,
  NotAllowed,
  EmailAlreadyUsed,
  IdentityProviderUnavailable
}

public sealed record CreateStaffResult(
  CreateStaffOutcome Outcome,
  CreatedStaffResponse? Staff,
  string? Message)
{
  public static CreateStaffResult Created(CreatedStaffResponse staff) =>
    new(CreateStaffOutcome.Created, staff, null);

  public static CreateStaffResult Failed(CreateStaffOutcome outcome, string message) =>
    new(outcome, null, message);
}

public interface IStaffProvisioningService
{
  /// <summary>
  /// Creates the login in the identity provider and the staff profile in
  /// one action, linked by the provider's user ID.
  /// </summary>
  Task<CreateStaffResult> CreateAsync(
    CreateStaffRequest request,
    CancellationToken cancellationToken);
}
