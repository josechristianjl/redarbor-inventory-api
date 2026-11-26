using InventoryManagement.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagement.Infrastructure.Persistence.Pagination;

internal static class PaginationHelper
{
    internal static async Task<PagedResult<T>> CreateAsync<T>(
        IQueryable<T> orderedQuery,
        PageRequest request,
        CancellationToken cancellationToken)
    {
        var totalCount = await orderedQuery.CountAsync(cancellationToken);
        long offset = ((long)request.Page - 1) * request.PageSize;
        if (offset >= totalCount)
        {
            return new PagedResult<T>(
                Array.Empty<T>(),
                request.Page,
                request.PageSize,
                totalCount);
        }
        var items = await orderedQuery
            .Skip((int)offset)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(
            items, request.Page, request.PageSize, totalCount);
    }
}
