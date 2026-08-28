using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.CompanyAdmins;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Domain.Identity;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.CompanyAdmins;

public sealed class CompanyAdminReadService : ICompanyAdminReadService
{
  private readonly CoreDbContext _db;

  public CompanyAdminReadService(CoreDbContext db)
  {
    _db = db;
  }

  public async Task<IReadOnlyList<CompanyAdminSummary>> ListActiveAsync(
    CancellationToken cancellationToken = default)
  {
    return await _db.StaffProfiles
      .AsNoTracking()
      .Where(profile =>
        profile.Role == Roles.CompanyAdmin &&
        profile.Status == HierarchyStatus.Active)
      .OrderBy(profile => profile.DisplayName)
      .Select(profile => new CompanyAdminSummary(
        profile.StaffProfileId,
        profile.DisplayName,
        profile.Email,
        profile.Status))
      .ToListAsync(cancellationToken);
  }
}
