using AERai.Web.Application.Common;

namespace AERai.Web.UI.Models;

/// <summary>
/// View helpers for <see cref="PagedResult{T}"/>.
/// </summary>
public static class PagedResultExtensions
{
    /// <summary>Builds the model for the shared pager partial.</summary>
    /// <typeparam name="T">Row type.</typeparam>
    /// <param name="result">The page of results.</param>
    /// <param name="search">The active search term.</param>
    /// <param name="filters">Other query-string filters to keep when paging.</param>
    /// <returns>The pager model.</returns>
    public static PagerViewModel ToPager<T>(this PagedResult<T> result, string? search, IReadOnlyDictionary<string, string>? filters = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new PagerViewModel(result.Page, result.TotalPages, result.TotalCount, search, filters);
    }
}
