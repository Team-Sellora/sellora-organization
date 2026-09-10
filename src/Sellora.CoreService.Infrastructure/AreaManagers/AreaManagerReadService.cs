using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.AreaManagers;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.AreaManagers;

public sealed class AreaManagerReadService : IAreaManagerReadService
{
  private readonly CoreDbContext _db;

  public AreaManagerReadService(CoreDbContext db)
  {
    _db = db;
  }

  public async Task<IReadOnlyList<AreaManagerSummary>> ListActiveAsync(
    CancellationToken cancellationToken = default)
  {
    // Company scope is automatic via CoreDbContext's ITenantScoped filter.
    return await _db.StaffProfiles
      .AsNoTracking()
      .Where(s =>
        s.Role == Roles.AreaManager &&
        s.Status == HierarchyStatus.Active)
      .OrderBy(s => s.DisplayName)
      .Select(s => new AreaManagerSummary(
        s.StaffProfileId,
        s.DisplayName,
        s.Email,
        s.Status))
      .ToListAsync(cancellationToken);
  }
}