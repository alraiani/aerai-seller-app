namespace AERai.Web.UI.Models;

/// <summary>
/// Data for the shared <c>_Pager</c> partial.
/// </summary>
/// <param name="Page">Current 1-based page.</param>
/// <param name="TotalPages">Total pages.</param>
/// <param name="TotalCount">Total matching rows.</param>
/// <param name="Search">Current search term, preserved in pager links.</param>
/// <param name="Filters">Other query-string filters to preserve in pager links (e.g. <c>family</c>).</param>
public sealed record PagerViewModel(int Page, int TotalPages, int TotalCount, string? Search, IReadOnlyDictionary<string, string>? Filters = null)
{
    /// <summary>Query-string values for a link to another page, keeping the search and filters.</summary>
    /// <param name="page">Target page.</param>
    /// <returns>Route data for <c>asp-all-route-data</c>.</returns>
    public Dictionary<string, string> RouteFor(int page)
    {
        var values = Filters is null ? new Dictionary<string, string>(StringComparer.Ordinal) : new Dictionary<string, string>(Filters, StringComparer.Ordinal);
        values["p"] = page.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!string.IsNullOrEmpty(Search))
        {
            values["q"] = Search;
        }

        return values;
    }
}
