using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Hierarchy;

public sealed class HierarchyDeactivationService : IHierarchyDeactivationService
{
  private readonly CoreDbContext _db;

  public HierarchyDeactivationService(CoreDbContext db)
  {
    _db = db;
  }

  public async Task<bool> DeactivateAgencyAsync(
    Guid agencyId,
    CancellationToken cancellationToken = default)
  {
    // The global tenant filter ensures an administrator cannot deactivate an agency belonging to another company.
    var agency = await _db.Agencies
      .SingleOrDefaultAsync(
        candidate => candidate.AgencyId == agencyId,
        cancellationToken);

    if (agency is null)
    {
      return false;
    }

    // Deactivation is idempotent. Repeating the request does not fail or create another historical operation.
    if (agency.Status == HierarchyStatus.Inactive)
    {
      return true;
    }

    agency.Status = HierarchyStatus.Inactive;

    await _db.SaveChangesAsync(cancellationToken);

    return true;
  }
}