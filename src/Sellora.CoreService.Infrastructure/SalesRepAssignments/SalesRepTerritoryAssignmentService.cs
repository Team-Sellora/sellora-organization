using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.SalesRepAssignments;

public sealed class SalesRepTerritoryAssignmentService
  : ISalesRepTerritoryAssignmentService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;
  private readonly ILogger<SalesRepTerritoryAssignmentService> _logger;

  public SalesRepTerritoryAssignmentService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ILogger<SalesRepTerritoryAssignmentService> logger)
  {
    _db = db;
    _currentUser = currentUser;
    _logger = logger;
  }

  public async Task<AssignSalesRepToTerritoryResult> AssignAsync(
    AssignSalesRepToTerritoryRequest request,
    CancellationToken cancellationToken = default)
  {
    var callerSub = _currentUser.Subject;

    if (string.IsNullOrWhiteSpace(callerSub))
    {
      return AssignSalesRepToTerritoryResult
        .CallerNotAnActiveAgencyOperator();
    }

    var operatorProfile = await _db.StaffProfiles.SingleOrDefaultAsync(
      profile =>
        profile.IdentitySub == callerSub &&
        profile.Role == Roles.AgencyOperator &&
        profile.Status == HierarchyStatus.Active,
      cancellationToken);

    if (operatorProfile is null)
    {
      return AssignSalesRepToTerritoryResult
        .CallerNotAnActiveAgencyOperator();
    }

    var operatorAgencyIds = await _db.AgencyOperatorAssignments
      .Where(assignment =>
        assignment.OperatorId == operatorProfile.StaffProfileId &&
        assignment.EndsAt == null)
      .Select(assignment => assignment.AgencyId)
      .Distinct()
      .ToListAsync(cancellationToken);

    if (operatorAgencyIds.Count == 0)
    {
      return AssignSalesRepToTerritoryResult
        .CallerNotAnActiveAgencyOperator();
    }

    var territory = await _db.Territories.SingleOrDefaultAsync(
      candidate =>
        candidate.TerritoryId == request.TerritoryId &&
        candidate.Status == HierarchyStatus.Active,
      cancellationToken);

    if (territory is null)
    {
      return AssignSalesRepToTerritoryResult
        .TerritoryNotFound(request.TerritoryId);
    }

    var territoryBelongsToOperatorAgency =
      await _db.TerritoryAgencyAssignments.AnyAsync(
        assignment =>
          assignment.TerritoryId == territory.TerritoryId &&
          assignment.EndsAt == null &&
          operatorAgencyIds.Contains(assignment.AgencyId),
        cancellationToken);

    if (!territoryBelongsToOperatorAgency)
    {
      _logger.LogWarning(
        "Rejected Sales Rep assignment: AgencyOperator {OperatorId} attempted " +
        "to assign SalesRep {SalesRepId} to territory {TerritoryId} outside " +
        "the operator's agency.",
        operatorProfile.StaffProfileId,
        request.SalesRepId,
        request.TerritoryId);

      return AssignSalesRepToTerritoryResult
        .TerritoryNotAssignedToCallerAgency(request.TerritoryId);
    }

    var salesRep = await _db.StaffProfiles.SingleOrDefaultAsync(
      profile =>
        profile.StaffProfileId == request.SalesRepId &&
        profile.Role == Roles.SalesRep &&
        profile.Status == HierarchyStatus.Active,
      cancellationToken);

    if (salesRep is null)
    {
      return AssignSalesRepToTerritoryResult
        .SalesRepNotFound(request.SalesRepId);
    }

    var territoryAssignment = await _db.SalesRepTerritoryAssignments
      .SingleOrDefaultAsync(
        assignment =>
          assignment.TerritoryId == territory.TerritoryId &&
          assignment.EndsAt == null,
        cancellationToken);

    if (territoryAssignment is not null)
    {
      var existingRep = await _db.StaffProfiles.SingleAsync(
        profile => profile.StaffProfileId == territoryAssignment.SalesRepId,
        cancellationToken);

      _logger.LogWarning(
        "Rejected Sales Rep assignment: AgencyOperator {OperatorId} attempted " +
        "to assign SalesRep {SalesRepId} to territory {TerritoryId}, but it " +
        "already has active SalesRep {ExistingSalesRepId}.",
        operatorProfile.StaffProfileId,
        request.SalesRepId,
        territory.TerritoryId,
        existingRep.StaffProfileId);

      return AssignSalesRepToTerritoryResult
        .TerritoryAlreadyHasActiveSalesRep(
          territory.Name,
          existingRep.DisplayName,
          existingRep.StaffProfileId);
    }

    var repAssignment = await _db.SalesRepTerritoryAssignments
      .SingleOrDefaultAsync(
        assignment =>
          assignment.SalesRepId == salesRep.StaffProfileId &&
          assignment.EndsAt == null,
        cancellationToken);

    if (repAssignment is not null)
    {
      var existingTerritory = await _db.Territories.SingleAsync(
        candidate => candidate.TerritoryId == repAssignment.TerritoryId,
        cancellationToken);

      _logger.LogWarning(
        "Rejected Sales Rep assignment: AgencyOperator {OperatorId} attempted " +
        "to assign SalesRep {SalesRepId} to territory {TerritoryId}, but the " +
        "rep already covers territory {ExistingTerritoryId}.",
        operatorProfile.StaffProfileId,
        salesRep.StaffProfileId,
        territory.TerritoryId,
        existingTerritory.TerritoryId);

      return AssignSalesRepToTerritoryResult
        .SalesRepAlreadyAssignedToTerritory(
          salesRep.DisplayName,
          existingTerritory.Code,
          existingTerritory.Name);
    }

    var assignment = new SalesRepTerritoryAssignment
    {
      AssignmentId = Guid.NewGuid(),
      CompanyId = territory.CompanyId,
      TerritoryId = territory.TerritoryId,
      SalesRepId = salesRep.StaffProfileId,
      StartsAt = DateTimeOffset.UtcNow,
      EndsAt = null,
      CreatedBy = callerSub
    };

    _db.SalesRepTerritoryAssignments.Add(assignment);

    try
    {
      await _db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception)
      when (exception.InnerException is PostgresException
      {
        SqlState: PostgresErrorCodes.UniqueViolation
      })
    {
      _logger.LogWarning(
        exception,
        "Rejected concurrent Sales Rep assignment: AgencyOperator {OperatorId} " +
        "attempted SalesRep {SalesRepId} to territory {TerritoryId}.",
        operatorProfile.StaffProfileId,
        salesRep.StaffProfileId,
        territory.TerritoryId);

      return AssignSalesRepToTerritoryResult.ConcurrentAssignmentConflict();
    }

    return AssignSalesRepToTerritoryResult.Success(assignment);
  }
}