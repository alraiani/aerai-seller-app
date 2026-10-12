using AERai.Web.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>
/// Paging helpers for EF Core queries.
/// </summary>
internal static class QueryableExtensions
{
    /// <summary>
    /// Executes a count and a single page of an already-ordered query.
    /// </summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="query">The query, which must already have a deterministic <c>OrderBy</c>.</param>
    /// <param name="request">Paging parameters.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The requested page.</returns>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageRequest request, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var items = await query.Skip(request.Skip).Take(request.SafePageSize).ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PagedResult<T>(items, total, request.SafePage, request.SafePageSize);
    }
}
