using AERai.Web.Application.Common;
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
}
