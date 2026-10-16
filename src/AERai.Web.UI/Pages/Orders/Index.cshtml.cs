using System.Text;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Orders;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Orders;

/// <summary>
/// Orders (from <c>rpt.vw_OrderSummary</c>): totals for what's listed, then the orders, newest first.
/// Each order expands into its items; it shows what Amazon paid out for it and the estimated profit
/// after cost of goods. Searchable by order id, SKU, ASIN, or title; filterable by local purchase date
/// and status; sortable by date, units, and sales; downloadable as a CSV.
/// </summary>
/// <param name="orders">Order list and export.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IOrderService orders, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Only orders placed on or after this local date, from <c>?from=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    /// <summary>Only orders placed on or before this local date, from <c>?to=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    /// <summary>Only orders with this status, from <c>?status=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "status")]
    public string? Status { get; set; }

    /// <summary>Sort from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public OrderSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The marketplace's time zone (purchase times are shown in it).</summary>
    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>
    /// The marketplace whose Settlements list holds this marketplace's payouts (Canada's are with the
    /// United States), or <see langword="null"/> when it keeps its own. Settlement links only work in the
    /// marketplace that lists them, so they're shown as plain text otherwise.
    /// </summary>
    public Domain.Core.Marketplace? SettlementsListedUnder { get; private set; }

    /// <summary>The orders, totals, and statuses.</summary>
    public OrderList List { get; private set; } = default!;

    /// <summary>Whether a search, date, or status filter is applied.</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || From is not null || To is not null || !string.IsNullOrWhiteSpace(Status);

    /// <summary>Loads the totals and the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var filter = await LoadFilterAsync(cancellationToken);
        List = await orders.GetListAsync(Marketplace, ToPageRequest(), filter, cancellationToken);
    }

    /// <summary>Downloads every order the search and filters select as a CSV, one row per item.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The file.</returns>
    public async Task<IActionResult> OnGetExportAsync(CancellationToken cancellationToken)
    {
        var filter = await LoadFilterAsync(cancellationToken);
        var export = await orders.ExportAsync(Marketplace, ToPageRequest(), filter, cancellationToken);
        // A byte-order mark makes Excel read the file as UTF-8, so titles with accents open correctly.
        return File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(export.Content)).ToArray(), "text/csv", export.FileName);
    }

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(OrderSort column) =>
        LinkWith(("sort", column == OrderSort.Purchased ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(OrderSort column) => SettlementFormat.Arrow(Sort == column, Sort.IsDescendingByDefault() != Descending);

    /// <summary>Link to the CSV with the current search and filters.</summary>
    /// <returns>A relative URL.</returns>
    public string ExportLink() => LinkWith(("handler", "Export"));

    private async Task<OrderListFilter> LoadFilterAsync(CancellationToken cancellationToken)
    {
        var selection = await currentMarketplace.GetAsync(cancellationToken);
        Marketplace = selection.Current;
        SettlementsListedUnder = Marketplace.SettlementMarketplaceId is { } listedUnder ? selection.All.FirstOrDefault(m => m.MarketplaceId == listedUnder) : null;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);

        // Dates are the marketplace's local days, like every other date filter in the app.
        return new OrderListFilter(
            From is { } from ? LocalTime.StartOfDay(from, Zone) : null,
            To is { } to ? LocalTime.StartOfDay(to.AddDays(1), Zone) : null,
            string.IsNullOrWhiteSpace(Status) ? null : Status.Trim(),
            Sort,
            Descending);
    }
}
