using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Application.Territories;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.SalesRepAssignments;

public sealed class SalesRepAssignmentReadService
  : ISalesRepAssignmentReadService
{
  private readonly CoreDbContext _db;
  private readonly ICurrentUserContext _currentUser;

  public SalesRepAssignmentReadService(
    CoreDbContext db,
    ICurrentUserContext currentUser)
  {
    _db = db;
    _currentUser = currentUser;
  }

  public async Task<IReadOnlyList<SalesRepSummary>> ListAsync(
    CancellationToken cancellationToken = default)
  {
    var agencyIds = await GetCallerAgencyIdsAsync(cancellationToken);

    if (agencyIds.Count == 0)
    {
      return Array.Empty<SalesRepSummary>();
    }

    return await _db.StaffProfiles
      .AsNoTracking()
      .Where(profile =>
        profile.Role == Roles.SalesRep &&
        profile.Status == HierarchyStatus.Active &&
        (
          // Unassigned reps can be selected for a new assignment.
          !_db.SalesRepTerritoryAssignments.Any(assignment =>
            assignment.SalesRepId == profile.StaffProfileId &&
            assignment.EndsAt == null)

          ||

          // Assigned reps are visible only when their active territory
          // belongs to one of the caller's agencies.
          _db.SalesRepTerritoryAssignments.Any(assignment =>
            assignment.SalesRepId == profile.StaffProfileId &&
            assignment.EndsAt == null &&
            _db.TerritoryAgencyAssignments.Any(territoryAgency =>
              territoryAgency.TerritoryId == assignment.TerritoryId &&
              territoryAgency.EndsAt == null &&
              agencyIds.Contains(territoryAgency.AgencyId)))
        ))
      .OrderBy(profile => profile.DisplayName)
      .Select(profile => new SalesRepSummary(
        profile.StaffProfileId,
        profile.DisplayName,
        profile.Email,
        profile.Status,
        _db.SalesRepTerritoryAssignments
          .Where(assignment =>
            assignment.SalesRepId == profile.StaffProfileId &&
            assignment.EndsAt == null &&
            _db.TerritoryAgencyAssignments.Any(territoryAgency =>
              territoryAgency.TerritoryId == assignment.TerritoryId &&
              territoryAgency.EndsAt == null &&
              agencyIds.Contains(territoryAgency.AgencyId)))
          .Join(
            _db.Territories,
            assignment => assignment.TerritoryId,
            territory => territory.TerritoryId,
            (assignment, territory) => new TerritoryResponse(
              territory.TerritoryId,
              territory.ProvinceId,
              territory.Code,
              territory.Name,
              territory.GeographicDescription,
              territory.Status,
              territory.CreatedAt))
          .FirstOrDefault()))
      .ToListAsync(cancellationToken);
  }

  public async Task<IReadOnlyList<TerritoryResponse>>
    ListUnassignedTerritoriesAsync(
      CancellationToken cancellationToken = default)
  {
    var agencyIds = await GetCallerAgencyIdsAsync(cancellationToken);

    if (agencyIds.Count == 0)
    {
      return Array.Empty<TerritoryResponse>();
    }

    return await _db.Territories
      .AsNoTracking()
      .Where(territory =>
        territory.Status == HierarchyStatus.Active &&
        _db.TerritoryAgencyAssignments.Any(assignment =>
          assignment.TerritoryId == territory.TerritoryId &&
          assignment.EndsAt == null &&
          agencyIds.Contains(assignment.AgencyId)) &&
        !_db.SalesRepTerritoryAssignments.Any(assignment =>
          assignment.TerritoryId == territory.TerritoryId &&
          assignment.EndsAt == null))
      .OrderBy(territory => territory.Code)
      .Select(territory => new TerritoryResponse(
        territory.TerritoryId,
        territory.ProvinceId,
        territory.Code,
        territory.Name,
        territory.GeographicDescription,
        territory.Status,
        territory.CreatedAt))
      .ToListAsync(cancellationToken);
  }

  private async Task<List<Guid>> GetCallerAgencyIdsAsync(
    CancellationToken cancellationToken)
  {
    var subject = _currentUser.Subject;

    if (string.IsNullOrWhiteSpace(subject))
    {
      return [];
    }

    var operatorId = await _db.StaffProfiles
      .Where(profile =>
        profile.IdentitySub == subject &&
        profile.Role == Roles.AgencyOperator &&
        profile.Status == HierarchyStatus.Active)
      .Select(profile => (Guid?)profile.StaffProfileId)
      .SingleOrDefaultAsync(cancellationToken);

    if (operatorId is null)
    {
      return [];
    }

    return await _db.AgencyOperatorAssignments
      .Where(assignment =>
        assignment.OperatorId == operatorId &&
        assignment.EndsAt == null)
      .Select(assignment => assignment.AgencyId)
      .Distinct()
      .ToListAsync(cancellationToken);
  }
}