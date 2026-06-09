using Microsoft.EntityFrameworkCore;
using PinballPVP.Api.Dtos;

namespace PinballPVP.Api.Extensions;

public static class QueryableExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int page, int pageSize, CancellationToken ct = default)
    {
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new PagedResult<T>(items, page, pageSize, totalCount);
    }
}
