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
  private readonly IRepTerritoryAssignmentCache _assignmentCache;

  public SalesRepTerritoryAssignmentService(
    CoreDbContext db,
    ICurrentUserContext currentUser,
    ILogger<SalesRepTerritoryAssignmentService> logger,
    IRepTerritoryAssignmentCache assignmentCache)
  {
    _db = db;
    _currentUser = currentUser;
    _logger = logger;
    _assignmentCache = assignmentCache;
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

    var repAssignment = await _db.SalesRepTerritoryAssignments
      .SingleOrDefaultAsync(
        assignment =>
          assignment.SalesRepId == salesRep.StaffProfileId &&
          assignment.EndsAt == null,
        cancellationToken);

    // PUT remains idempotent when the requested binding already exists.
    if (territoryAssignment is not null &&
        territoryAssignment.SalesRepId == salesRep.StaffProfileId)
    {
      return AssignSalesRepToTerritoryResult.Success(territoryAssignment);
    }

    var assignmentsToEnd = new[] { territoryAssignment, repAssignment }
      .Where(assignment => assignment is not null)
      .Select(assignment => assignment!)
      .DistinctBy(assignment => assignment.AssignmentId)
      .ToList();

    var now = DateTimeOffset.UtcNow;

    await using var transaction = await _db.Database.BeginTransactionAsync(
      cancellationToken);

    try
    {
      foreach (var activeAssignment in assignmentsToEnd)
      {
        activeAssignment.EndsAt = now;
      }

      var assignment = new SalesRepTerritoryAssignment
      {
        AssignmentId = Guid.NewGuid(),
        CompanyId = territory.CompanyId,
        TerritoryId = territory.TerritoryId,
        SalesRepId = salesRep.StaffProfileId,
        StartsAt = now,
        EndsAt = null,
        CreatedBy = callerSub
      };

      _db.SalesRepTerritoryAssignments.Add(assignment);

      await _db.SaveChangesAsync(cancellationToken);
      await transaction.CommitAsync(cancellationToken);

      foreach (var endedAssignment in assignmentsToEnd)
      {
        _assignmentCache.Invalidate(
          endedAssignment.SalesRepId,
          endedAssignment.TerritoryId);
      }

      _assignmentCache.Invalidate(
        assignment.SalesRepId,
        assignment.TerritoryId);

      _logger.LogInformation(
        "Sales Rep {SalesRepId} assigned to territory {TerritoryId}; " +
        "{EndedAssignmentCount} previous active binding(s) preserved as history.",
        salesRep.StaffProfileId,
        territory.TerritoryId,
        assignmentsToEnd.Count);

      return AssignSalesRepToTerritoryResult.Success(assignment);
    }
    catch (DbUpdateException exception)
      when (exception.InnerException is PostgresException
      {
        SqlState: PostgresErrorCodes.UniqueViolation
      })
    {
      await transaction.RollbackAsync(cancellationToken);

      _logger.LogWarning(
        exception,
        "Concurrent Sales Rep reassignment conflict for SalesRep {SalesRepId} " +
        "and territory {TerritoryId}.",
        salesRep.StaffProfileId,
        territory.TerritoryId);

      return AssignSalesRepToTerritoryResult.ConcurrentAssignmentConflict();
    }
    catch
    {
      await transaction.RollbackAsync(cancellationToken);
      throw;
    }
  }
}