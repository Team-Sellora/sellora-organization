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

    if (!string.Equals(target.Role, Roles.AreaManager, StringComparison.Ordinal))
    {
      return AssignAreaManagerResult.TargetNotAreaManager(target);
    }

    if (!string.Equals(
      target.Status,
      HierarchyStatus.Active,
      StringComparison.Ordinal))
    {
      return AssignAreaManagerResult.TargetInactive(target);
    }

    // The current active assignment for this province, if any. The partial
    // unique index (UNIQUE (province_id) WHERE ends_at IS NULL) guarantees
    // at most one row here.
    var currentForProvince = await _db.ProvinceManagerAssignments
      .SingleOrDefaultAsync(
        a => a.ProvinceId == request.ProvinceId && a.EndsAt == null,
        cancellationToken);

    // Idempotent path: the target is already this province's active manager.
    // Return the existing row rather than appending a duplicate history row
    // — this makes the PUT safe to retry after a network glitch or double-
    // click, which is the whole point of PUT being an idempotent verb.
    if (currentForProvince is not null &&
        currentForProvince.AreaManagerId == request.AreaManagerId)
    {
      return AssignAreaManagerResult.Success(currentForProvince);
    }

    // 409: the target is currently the active manager of a *different*
    // province. An Area Manager may cover multiple provinces, but the story
    // treats that as a decision the caller must make explicitly — the
    // endpoint refuses the implicit spread and names the other province.
    var elsewhereForSameUser = await _db.ProvinceManagerAssignments
      .FirstOrDefaultAsync(
        a => a.AreaManagerId == request.AreaManagerId &&
             a.ProvinceId != request.ProvinceId &&
             a.EndsAt == null,
        cancellationToken);

    if (elsewhereForSameUser is not null)
    {
      return AssignAreaManagerResult.TargetAlreadyManagesAnotherProvince(
        target,
        elsewhereForSameUser.ProvinceId);
    }

    var actingSub = _currentUser.Subject
      ?? throw new InvalidOperationException(
        "The current request has no subject claim.");

    // End-then-create in a single transaction. Both writes go through a
    // single SaveChangesAsync (EF wraps that call in an implicit transaction
    // on its own), and the explicit BeginTransactionAsync makes the atomicity
    // of both writes durable against future code that might split them
    // across multiple SaveChanges calls.
    //
    // PostgreSQL is the last line of defence: the partial unique index
    // (UNIQUE (province_id) WHERE ends_at IS NULL) means a race between
    // two admins is caught by the database, not silently allowed.
    var now = DateTimeOffset.UtcNow;
    await using var tx = await _db.Database.BeginTransactionAsync(
      cancellationToken);

    if (currentForProvince is not null)
    {
      currentForProvince.EndsAt = now;
    }

    var assignment = new ProvinceManagerAssignment
    {
      AssignmentId = Guid.NewGuid(),
      CompanyId = province.CompanyId,
      ProvinceId = province.ProvinceId,
      AreaManagerId = target.StaffProfileId,
      ReportsToAdminId = null,
      StartsAt = now,
      EndsAt = null,
      CreatedBy = actingSub
    };

    _db.ProvinceManagerAssignments.Add(assignment);
    await _db.SaveChangesAsync(cancellationToken);
    await tx.CommitAsync(cancellationToken);

    return AssignAreaManagerResult.Success(assignment);
  }
}