using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Models;

/// <summary>
/// Partial reloads: a page section marked <c>data-fragment</c> is refreshed by site.js, which requests the
/// page's own URL with the <see cref="Header"/> header and swaps in just that section's partial.
/// </summary>
/// <remarks>
/// A header rather than a <c>?handler=</c> keeps the fetched URL identical to the page's real URL, so links
/// built from the current query (<see cref="ListPageModel.LinkWith"/>), browser history, bookmarks, and the
/// no-script path all stay the same. The page handler loads exactly what it loads for a full page.
/// </remarks>
public static class PageFragments
{
    /// <summary>Request header site.js sends when it wants only the page's fragment.</summary>
    public const string Header = "X-Fragment";

    /// <summary>Whether the request asked for the fragment rather than the full page.</summary>
    /// <param name="page">The page handling the request.</param>
    /// <returns><see langword="true"/> for a partial reload.</returns>
    public static bool IsFragmentRequest(this PageModel page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return page.Request.Headers.ContainsKey(Header);
    }

    /// <summary>
    /// The full page, or only <paramref name="partialName"/> (rendered with the page model) when site.js
    /// asked for the fragment.
    /// </summary>
    /// <param name="page">The page handling the request.</param>
    /// <param name="partialName">The partial that renders the page's <c>data-fragment</c> section.</param>
    /// <returns>The page or the partial.</returns>
    public static IActionResult PageOrFragment(this PageModel page, string partialName)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(partialName);

        // Both answers share one URL: the cache must key on the header, and a fragment must never be what
        // the browser shows when someone goes Back to the page or reopens it.
        page.Response.Headers.Vary = Header;
        if (!page.IsFragmentRequest())
        {
            return page.Page();
        }

        page.Response.Headers.CacheControl = "no-store";
        return page.Partial(partialName, page);
    }
}
