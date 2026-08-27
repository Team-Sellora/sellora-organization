using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Sellora.CoreService.Application.SalesRepAssignments;
using Sellora.CoreService.Infrastructure.Persistence;

namespace Sellora.CoreService.Infrastructure.SalesRepAssignments;

public sealed class MemoryRepTerritoryAssignmentCache
  : IRepTerritoryAssignmentCache
{
  private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

  private readonly IMemoryCache _cache;
  private readonly CoreDbContext _db;

  public MemoryRepTerritoryAssignmentCache(
    IMemoryCache cache,
    CoreDbContext db)
  {
    _cache = cache;
    _db = db;
  }

  public async Task<Guid?> GetActiveTerritoryIdAsync(
    Guid salesRepId,
    CancellationToken cancellationToken = default)
  {
    var entry = await _cache.GetOrCreateAsync(
      $"rep-territory:rep:{salesRepId}",
      async cacheEntry =>
      {
        cacheEntry.AbsoluteExpirationRelativeToNow = Ttl;

        var territoryId = await _db.SalesRepTerritoryAssignments
          .AsNoTracking()
          .Where(assignment =>
            assignment.SalesRepId == salesRepId &&
            assignment.EndsAt == null)
          .Select(assignment => (Guid?)assignment.TerritoryId)
          .SingleOrDefaultAsync(cancellationToken);

        return new CachedLookup(territoryId);
      });

    return entry!.Value;
  }

  public async Task<Guid?> GetActiveSalesRepIdAsync(
    Guid territoryId,
    CancellationToken cancellationToken = default)
  {
    var entry = await _cache.GetOrCreateAsync(
      $"rep-territory:territory:{territoryId}",
      async cacheEntry =>
      {
        cacheEntry.AbsoluteExpirationRelativeToNow = Ttl;

        var salesRepId = await _db.SalesRepTerritoryAssignments
          .AsNoTracking()
          .Where(assignment =>
            assignment.TerritoryId == territoryId &&
            assignment.EndsAt == null)
          .Select(assignment => (Guid?)assignment.SalesRepId)
          .SingleOrDefaultAsync(cancellationToken);

        return new CachedLookup(salesRepId);
      });

    return entry!.Value;
  }

  public void Invalidate(Guid salesRepId, Guid territoryId)
  {
    _cache.Remove($"rep-territory:rep:{salesRepId}");
    _cache.Remove($"rep-territory:territory:{territoryId}");
  }

  private sealed record CachedLookup(Guid? Value);
}