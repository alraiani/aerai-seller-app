using AERai.Web.Application.Common;
using System.Linq.Expressions;
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

    /// <summary>Sorts by a key in the given direction, for sorts the user can reverse.</summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <param name="query">The query.</param>
    /// <param name="key">Sort key.</param>
    /// <param name="descending">Whether to sort high to low.</param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<T> OrderBy<T, TKey>(this IQueryable<T> query, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? query.OrderByDescending(key) : query.OrderBy(key);

    /// <summary>Adds a secondary sort key in the given direction.</summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <typeparam name="TKey">Key type.</typeparam>
    /// <param name="query">The ordered query.</param>
    /// <param name="key">Sort key.</param>
    /// <param name="descending">Whether to sort high to low.</param>
    /// <returns>The ordered query.</returns>
    public static IOrderedQueryable<T> ThenBy<T, TKey>(this IOrderedQueryable<T> query, Expression<Func<T, TKey>> key, bool descending) =>
        descending ? query.ThenByDescending(key) : query.ThenBy(key);
}
