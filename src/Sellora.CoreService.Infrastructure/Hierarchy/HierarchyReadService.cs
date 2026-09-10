using Microsoft.EntityFrameworkCore;
using Sellora.CoreService.Application.Hierarchy;
using Sellora.CoreService.Application.Identity;
using Sellora.CoreService.Domain.Entities;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.Hierarchy;

/// <summary>
/// Coordinates tenant lookup, role-scope resolution, database loading,
/// and response-tree construction.
/// </summary>
public sealed class HierarchyReadService
  : IHierarchyReadService
{
  private readonly CoreDbContext _db;
  private readonly HierarchyScopeResolver _scopeResolver;
  private readonly HierarchyDataLoader _dataLoader;

  public HierarchyReadService(
    CoreDbContext db,
    ICurrentUserContext currentUser)
  {
    _db = db;
    _scopeResolver = new HierarchyScopeResolver(
      db,
      currentUser);
    _dataLoader = new HierarchyDataLoader(db);
  }

  public async Task<HierarchyTreeResponse?>
    GetHierarchyAsync(
      CancellationToken cancellationToken = default)
  {
    // CoreDbContext applies CompanyId from the authenticated JWT.
    // The client cannot select a company through a query parameter.
    var company = await _db.Companies
      .AsNoTracking()
      .SingleOrDefaultAsync(
        candidate =>
          candidate.Status == HierarchyStatus.Active,
        cancellationToken);

    if (company is null)
    {
      return null;
    }

    var visibility = await _scopeResolver.ResolveAsync(
      cancellationToken);

    var data = await _dataLoader.LoadAsync(
      visibility,
      cancellationToken);

    return HierarchyTreeBuilder.Build(company, data);
  }
}