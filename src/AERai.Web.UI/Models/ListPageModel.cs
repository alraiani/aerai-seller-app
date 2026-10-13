using AERai.Web.Application.Common;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Models;

/// <summary>
/// Base page model for paged, searchable list pages. Binds <c>?p=</c> (page) and <c>?q=</c> (search)
/// from the query string so every list page pages and filters the same way.
/// </summary>
public abstract class ListPageModel : PageModel
{
    /// <summary>1-based page number from <c>?p=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "p")]
    public int PageNumber { get; set; } = 1;

    /// <summary>Free-text filter from <c>?q=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Search { get; set; }

    /// <summary>The bound values as an Application-layer <see cref="PageRequest"/>.</summary>
    /// <returns>The page request.</returns>
    protected PageRequest ToPageRequest() => new(PageNumber, PageRequest.DefaultPageSize, Search);

    /// <summary>
    /// A link to this page (same route, e.g. the settlement id) with the current query string, some
    /// parameters changed. A <see langword="null"/> or empty value removes that parameter (back to its
    /// default). Paging restarts at page 1 because a new filter or sort changes what page 1 holds.
    /// </summary>
    /// <param name="changes">Query parameters to set or clear.</param>
    /// <returns>A relative URL.</returns>
    public string LinkWith(params (string Key, string? Value)[] changes)
    {
        var values = Request.Query
            .Where(q => q.Key != "p")
            .ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in changes)
        {
            if (string.IsNullOrEmpty(value))
            {
                values.Remove(key);
            }
            else
            {
                values[key] = value;
            }
        }

        return Request.PathBase + Request.Path + new QueryBuilder(values.Where(v => !string.IsNullOrEmpty(v.Value))).ToQueryString();
    }

    /// <summary>The query-string values (other than page and search) to keep when paging.</summary>
    /// <returns>The current filters and sort.</returns>
    public IReadOnlyDictionary<string, string> CurrentFilters() =>
        Request.Query.Where(q => q.Key is not ("p" or "q")).ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
}
