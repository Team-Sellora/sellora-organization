using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.TerritoryAssignments;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.TerritoryAssignments;

public sealed class TerritoryAgencyAssignmentService
  : ITerritoryAgencyAssignmentService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly ILogger<TerritoryAgencyAssignmentService> _logger;

  public TerritoryAgencyAssignmentService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ILogger<TerritoryAgencyAssignmentService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _logger = logger;
  }

  public async Task<AssignTerritoryAgencyResult> AssignAsync(
    AssignTerritoryAgencyRequest request,
    CancellationToken cancellationToken = default)
  {
    var callerSub = _currentUser.Subject;

    var manager = await _db.StaffProfiles.SingleOrDefaultAsync(
      profile =>
        profile.IdentitySub == callerSub &&
        profile.Role == Roles.AreaManager &&
        profile.Status == HierarchyStatus.Active,
      cancellationToken);

    if (manager is null)
    {
      return AssignTerritoryAgencyResult.CallerNotAnActiveAreaManager();
    }

    var territory = await _db.Territories.SingleOrDefaultAsync(
      item => item.TerritoryId == request.TerritoryId,
      cancellationToken);

    if (territory is null)
    {
      return AssignTerritoryAgencyResult.TerritoryNotFound(request.TerritoryId);
    }

    var agency = await _db.Agencies.SingleOrDefaultAsync(
      item => item.AgencyId == request.AgencyId,
      cancellationToken);

    if (agency is null)
    {
      return AssignTerritoryAgencyResult.AgencyNotFound(request.AgencyId);
    }

    var managedProvinceIds = await _db.ProvinceManagerAssignments
      .Where(assignment =>
        assignment.AreaManagerId == manager.StaffProfileId &&
        assignment.EndsAt == null)
      .Select(assignment => assignment.ProvinceId)
      .ToHashSetAsync(cancellationToken);

    if (!managedProvinceIds.Contains(territory.ProvinceId))
    {
      _logger.LogWarning(
        "Rejected territory-agency assignment: AreaManager {AreaManagerId} " +
        "attempted territory {TerritoryId} to agency {AgencyId}; territory " +
        "is not in the manager's provinces.",
        manager.StaffProfileId,
        request.TerritoryId,
        request.AgencyId);

      return AssignTerritoryAgencyResult.TerritoryNotInManagedProvinces(
        request.TerritoryId);
    }

    if (!managedProvinceIds.Contains(agency.ProvinceId))
    {
      _logger.LogWarning(
        "Rejected territory-agency assignment: AreaManager {AreaManagerId} " +
        "attempted territory {TerritoryId} to agency {AgencyId}; agency " +
        "is not in the manager's provinces.",
        manager.StaffProfileId,
        request.TerritoryId,
        request.AgencyId);

      return AssignTerritoryAgencyResult.AgencyNotInManagedProvinces(
        request.AgencyId);
    }

    if (territory.ProvinceId != agency.ProvinceId)
    {
      _logger.LogWarning(
        "Rejected territory-agency assignment: AreaManager {AreaManagerId} " +
        "attempted territory {TerritoryId} to agency {AgencyId}; entities " +
        "belong to different provinces.",
        manager.StaffProfileId,
        request.TerritoryId,
        request.AgencyId);

      return AssignTerritoryAgencyResult.AgencyNotInTerritoryProvince(
        request.TerritoryId,
        request.AgencyId);
    }

    var currentAssignment = await _db.TerritoryAgencyAssignments
      .SingleOrDefaultAsync(
        assignment =>
          assignment.TerritoryId == territory.TerritoryId &&
          assignment.EndsAt == null,
        cancellationToken);

    // PUT is idempotent: retrying the current assignment must not append
    // another history row.
    if (currentAssignment is not null &&
        currentAssignment.AgencyId == agency.AgencyId)
    {
      return AssignTerritoryAgencyResult.Success(currentAssignment);
    }

    var now = DateTimeOffset.UtcNow;

    // Ending the old row and inserting the new row must be atomic. The
    // partial unique index remains the database-level guarantee that only
    // one active assignment exists for a territory.
    await using var transaction = await _db.Database.BeginTransactionAsync(
      cancellationToken);

    if (currentAssignment is not null)
    {
      currentAssignment.EndsAt = now;
    }

    var assignment = new TerritoryAgencyAssignment
    {
      AssignmentId = Guid.NewGuid(),
      CompanyId = territory.CompanyId,
      TerritoryId = territory.TerritoryId,
      AgencyId = agency.AgencyId,
      StartsAt = now,
      EndsAt = null,
      CreatedBy = callerSub!
    };

    _db.TerritoryAgencyAssignments.Add(assignment);

    await _db.SaveChangesAsync(cancellationToken);
    await transaction.CommitAsync(cancellationToken);

    return AssignTerritoryAgencyResult.Success(assignment);
  }
}