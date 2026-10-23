using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.IdentityProvisioning;
using Sellora.CoreService.Application.Staff;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Domain.Tenancy;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Staff;

/// <summary>
/// POST /api/staff: one action creates both the login (WSO2 IS) and the
/// staff profile, linked by the provider's user ID. Nobody copies IDs.
///
/// Order: validate → create the login → save the profile. If the save
/// fails, the login is deleted so no orphan account is left behind.
/// </summary>
public sealed class StaffProvisioningService : IStaffProvisioningService
{
  // Who may create whom. Area Managers and Agency Operators are then linked
  // to provinces/agencies through the existing assignment screens.
  private static readonly Dictionary<string, string[]> CreatableRoles =
    new Dictionary<string, string[]>
    {
      [Roles.CompanyAdmin] = new[] { Roles.CompanyAdmin, Roles.AreaManager, Roles.AgencyOperator, Roles.SalesRep },
      [Roles.AgencyOperator] = new[] { Roles.SalesRep }
    };

  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly ITenantContext _tenant;
  private readonly IIdentityProvisioner _provisioner;
  private readonly ILogger<StaffProvisioningService> _logger;

  public StaffProvisioningService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ITenantContext tenant,
    IIdentityProvisioner provisioner,
    ILogger<StaffProvisioningService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _tenant = tenant;
    _provisioner = provisioner;
    _logger = logger;
  }

  public async Task<CreateStaffResult> CreateAsync(
    CreateStaffRequest request,
    CancellationToken cancellationToken)
  {
    var callerRole = _currentUser.Role;
    var companyId = _tenant.CompanyId;

    if (companyId is null || callerRole is null || !CreatableRoles.TryGetValue(callerRole, out var allowed))
    {
      return CreateStaffResult.Failed(
        CreateStaffOutcome.NotAllowed,
        "Only Company Admins and Agency Operators can add staff.");
    }

    var role = request.Role?.Trim();
    var displayName = request.DisplayName?.Trim();
    var email = request.Email?.Trim().ToLowerInvariant();
    var phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();

    if (string.IsNullOrEmpty(role) || !allowed.Contains(role))
    {
      return CreateStaffResult.Failed(
        RoleRejectionOutcome(role),
        $"As {callerRole} you can add: {string.Join(", ", allowed)}.");
    }

    if (string.IsNullOrEmpty(displayName) || displayName.Length > 200)
    {
      return CreateStaffResult.Failed(CreateStaffOutcome.InvalidRequest, "displayName is required (max 200 characters).");
    }

    if (string.IsNullOrEmpty(email) || email.Length > 320 || !IsEmail(email))
    {
      return CreateStaffResult.Failed(CreateStaffOutcome.InvalidRequest, "A valid email is required; it becomes the login username.");
    }

    if (phone is { Length: > 40 })
    {
      return CreateStaffResult.Failed(CreateStaffOutcome.InvalidRequest, "phone must be at most 40 characters.");
    }

    // Tenant-filtered: another company's staff with the same email is
    // invisible here, and IS itself refuses a duplicate username.
    if (await _db.StaffProfiles.AnyAsync(profile => profile.Email == email, cancellationToken))
    {
      return CreateStaffResult.Failed(CreateStaffOutcome.EmailAlreadyUsed, $"A staff member with email {email} already exists.");
    }

    var (identity, provisioningFailure) = await ProvisionLoginAsync(
      new NewIdentityUser(email, displayName, phone, role, companyId.Value),
      cancellationToken);

    if (identity is null)
    {
      return provisioningFailure!;
    }

    var profile = new StaffProfile
    {
      StaffProfileId = Guid.NewGuid(),
      CompanyId = companyId.Value,
      IdentitySub = identity.IdentityId,
      Role = role,
      DisplayName = displayName,
      Email = email,
      Phone = phone,
      Status = HierarchyStatus.Active,
      CreatedAt = DateTimeOffset.UtcNow
    };

    _db.StaffProfiles.Add(profile);

    try
    {
      await _db.SaveChangesAsync(CancellationToken.None);
    }
    catch (Exception exception)
    {
      _logger.LogError(exception, "Saving staff profile for {Email} failed; removing the new login", email);
      await _provisioner.DeleteUserAsync(identity.IdentityId, CancellationToken.None);
      throw;
    }

    _logger.LogInformation(
      "{CallerRole} {CallerSub} added {Role} {Email} as staff profile {StaffProfileId}",
      callerRole, _currentUser.Subject, role, email, profile.StaffProfileId);

    return CreateStaffResult.Created(new CreatedStaffResponse(
      profile.StaffProfileId,
      profile.Role,
      profile.DisplayName,
      email,
      profile.Phone,
      profile.Status,
      profile.IdentitySub,
      identity.UserName,
      identity.TemporaryPassword));
  }

  private async Task<(ProvisionedIdentity? Identity, CreateStaffResult? Failure)> ProvisionLoginAsync(
    NewIdentityUser user,
    CancellationToken cancellationToken)
  {
    try
    {
      return (await _provisioner.CreateUserAsync(user, cancellationToken), null);
    }
    catch (IdentityUserAlreadyExistsException)
    {
      return (null, CreateStaffResult.Failed(
        CreateStaffOutcome.EmailAlreadyUsed,
        $"A login for {user.Email} already exists in the identity provider."));
    }
    catch (IdentityProvisioningUnavailableException exception)
    {
      return (null, CreateStaffResult.Failed(CreateStaffOutcome.IdentityProviderUnavailable, exception.Message));
    }
  }

  private static CreateStaffOutcome RoleRejectionOutcome(string? role) =>
    role is not null && IsKnownRole(role) ? CreateStaffOutcome.NotAllowed : CreateStaffOutcome.InvalidRequest;

  private static bool IsKnownRole(string role) =>
    role is Roles.CompanyAdmin or Roles.AreaManager or Roles.AgencyOperator or Roles.SalesRep or Roles.ShopOwner;

  private static bool IsEmail(string value) =>
    MailAddress.TryCreate(value, out var address) && address.Address == value;
}
