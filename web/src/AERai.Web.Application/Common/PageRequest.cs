using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Application.Common;

/// <summary>
/// Paging and free-text search parameters for list queries.
/// </summary>
/// <param name="Page">1-based page number; values below 1 are treated as 1.</param>
/// <param name="PageSize">Rows per page, clamped to <see cref="MaxPageSize"/>.</param>
/// <param name="Search">Optional case-insensitive filter applied by the query.</param>
public sealed record PageRequest(int Page = 1, [property: Range(1, PageRequest.MaxPageSize)] int PageSize = PageRequest.DefaultPageSize, string? Search = null)
{
    /// <summary>Default rows per page.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Upper bound on rows per page, protecting the database from unbounded reads.</summary>
    public const int MaxPageSize = 200;

    /// <summary>The page number, normalized to at least 1.</summary>
    public int SafePage => Math.Max(1, Page);

    /// <summary>The page size, normalized to 1..<see cref="MaxPageSize"/>.</summary>
    public int SafePageSize => Math.Clamp(PageSize, 1, MaxPageSize);

    /// <summary>Number of rows to skip for the current page.</summary>
    public int Skip => (SafePage - 1) * SafePageSize;

    /// <summary>The trimmed search term, or <see langword="null"/> when blank.</summary>
    public string? SafeSearch => string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
}
