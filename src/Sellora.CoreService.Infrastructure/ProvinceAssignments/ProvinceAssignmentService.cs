using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.ProvinceAssignments;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.ProvinceAssignments;

public sealed class ProvinceAssignmentService : IProvinceAssignmentService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;

  public ProvinceAssignmentService(
    CoreDbContext db,
    ICurrentUserContext currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public async Task<AssignAreaManagerResult> AssignAreaManagerAsync(
    AssignAreaManagerRequest request,
    CancellationToken cancellationToken = default)
  {
    // The global tenant filter scopes every query below to the caller's
    // company, so a province or user belonging to another company simply
    // resolves to null here — that is the tenant half of the validation.

    var province = await _db.Provinces
      .SingleOrDefaultAsync(
        p => p.ProvinceId == request.ProvinceId,
        cancellationToken);

    if (province is null)
    {
      return AssignAreaManagerResult.ProvinceNotFound(request.ProvinceId);
    }

    var target = await _db.StaffProfiles
      .SingleOrDefaultAsync(
        s => s.StaffProfileId == request.AreaManagerId,
        cancellationToken);

    if (target is null)
    {
      return AssignAreaManagerResult.TargetUserNotFound(request.AreaManagerId);
    }

    // Role check — a specific 400 that names the actual role, not a
    // generic validation error, so the admin sees exactly why (AC3).
    if (!string.Equals(target.Role, Roles.AreaManager, StringComparison.Ordinal))
    {
      return AssignAreaManagerResult.TargetNotAreaManager(target);
    }

    if (!string.Equals(target.Status, HierarchyStatus.Active, StringComparison.Ordinal))
    {
      return AssignAreaManagerResult.TargetInactive(target);
    }

    // Defensive check for CSP-62 scope: with the partial unique index
    // (UNIQUE (province_id) WHERE ends_at IS NULL) on the table, inserting
    // a second active row would throw a 500-shaped constraint violation.
    // CSP-63 replaces this branch with the transactional end-then-create
    // reassignment logic.
    var hasActiveManager = await _db.ProvinceManagerAssignments
      .AnyAsync(
        a => a.ProvinceId == request.ProvinceId && a.EndsAt == null,
        cancellationToken);

    if (hasActiveManager)
    {
      return AssignAreaManagerResult.ProvinceAlreadyHasActiveManager(
        request.ProvinceId);
    }

    var actingSub = _currentUser.Subject
      ?? throw new InvalidOperationException(
        "The current request has no subject claim.");

    var assignment = new ProvinceManagerAssignment
    {
      AssignmentId = Guid.NewGuid(),
      CompanyId = province.CompanyId,   // authoritative — tenant-filtered row
      ProvinceId = province.ProvinceId,
      AreaManagerId = target.StaffProfileId,
      ReportsToAdminId = null,           // populated in US-E1-8
      StartsAt = DateTimeOffset.UtcNow,
      EndsAt = null,                     // null == active
      CreatedBy = actingSub              // audit: which admin did this
    };

    _db.ProvinceManagerAssignments.Add(assignment);
    await _db.SaveChangesAsync(cancellationToken);

    return AssignAreaManagerResult.Success(assignment);
  }
}