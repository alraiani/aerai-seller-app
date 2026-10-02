namespace AERai.Web.UI.Models;

/// <summary>
/// Data for the shared <c>_Pager</c> partial.
/// </summary>
/// <param name="Page">Current 1-based page.</param>
/// <param name="TotalPages">Total pages.</param>
/// <param name="TotalCount">Total matching rows.</param>
/// <param name="Search">Current search term, preserved in pager links.</param>
public sealed record PagerViewModel(int Page, int TotalPages, int TotalCount, string? Search);
