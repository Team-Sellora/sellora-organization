using Microsoft.EntityFrameworkCore;

namespace Sellora.CoreService.Infrastructure.Persistence;

public static class QueryableExtensions
{
    public static async Task<HashSet<T>> ToHashSetAsync<T>(
        this IQueryable<T> source,
        CancellationToken cancellationToken = default)
    {
        var list = await source.ToListAsync(cancellationToken);
        return list.ToHashSet();
    }
}
