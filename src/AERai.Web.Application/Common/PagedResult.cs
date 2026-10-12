namespace AERai.Web.Application.Common;

/// <summary>
/// One page of query results plus the information needed to render a pager.
/// </summary>
/// <typeparam name="T">Row type.</typeparam>
/// <param name="Items">Rows on this page.</param>
/// <param name="TotalCount">Rows matching the query across all pages.</param>
/// <param name="Page">The 1-based page number returned.</param>
/// <param name="PageSize">The page size used.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    /// <summary>Total number of pages (at least 1, so an empty result still renders a pager).</summary>
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));

    /// <summary>Whether a previous page exists.</summary>
    public bool HasPrevious => Page > 1;

    /// <summary>Whether a next page exists.</summary>
    public bool HasNext => Page < TotalPages;
}
