using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Settlements;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// Payouts Amazon deferred for the current marketplace: what it still holds (with when each releases) or,
/// switched, what it released in the last 30 days. Searchable by order id; sortable by release date,
/// amount, and posted date.
/// </summary>
/// <param name="deferred">Deferred payouts.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class DeferredModel(IDeferredPaymentService deferred, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Still held or recently released, from <c>?view=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "view")]
    public DeferredView View { get; set; }

    /// <summary>Sort from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public DeferredSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The marketplace's time zone, in which dates are shown.</summary>
    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>What Amazon holds now (the tile at the top).</summary>
    public DeferredOverview Overview { get; private set; } = new([], new DeferredSyncState(false, false, null), DateTimeOffset.MinValue);

    /// <summary>The current page of transactions.</summary>
    public PagedResult<DeferredPaymentRow> Rows { get; private set; } = default!;

    /// <summary>Loads the overview and the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);
        Overview = await deferred.GetOverviewAsync(Marketplace, cancellationToken);
        Rows = await deferred.GetPageAsync(Marketplace, ToPageRequest(), new DeferredListFilter(View, Sort, Descending), cancellationToken);
    }

    /// <summary>Link to a view, keeping the search.</summary>
    /// <param name="view">View.</param>
    /// <returns>A relative URL.</returns>
    public string ViewLink(DeferredView view) => LinkWith(("view", view == DeferredView.Deferred ? null : view.ToString()), ("p", null));

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(DeferredSort column) =>
        LinkWith(("sort", column == DeferredSort.Maturity ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(DeferredSort column) =>
        SettlementFormat.Arrow(Sort == column, (column != DeferredSort.Maturity) != Descending);
}
