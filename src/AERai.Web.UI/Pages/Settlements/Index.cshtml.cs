using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// Settlement payouts (from <c>rpt.vw_SettlementSummary</c>): totals for what's listed, then the
/// settlements, newest period first. Searchable by settlement, order, SKU, ASIN, or title; filterable
/// by period end; sortable by period and each amount column.
/// </summary>
/// <param name="reporting">Reporting queries.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IReportingQueries reporting, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Only periods ending on or after this local date, from <c>?from=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    /// <summary>Only periods ending on or before this local date, from <c>?to=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    /// <summary>Sort from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public SettlementSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The current page of settlements.</summary>
    public PagedResult<SettlementSummary> Settlements { get; private set; } = default!;

    /// <summary>Totals over every settlement the search and filters select.</summary>
    public SettlementTotals Totals { get; private set; } = SettlementTotals.Empty;

    /// <summary>Whether a search or date filter is applied.</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || From is not null || To is not null;

    /// <summary>Loads the totals and the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;

        // Dates are the marketplace's local days, like every other date filter in the app.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);
        var filter = new SettlementListFilter(
            From is { } from ? LocalTime.StartOfDay(from, zone) : null,
            To is { } to ? LocalTime.StartOfDay(to.AddDays(1), zone) : null,
            Sort,
            Descending);
        var request = ToPageRequest();
        Totals = await reporting.GetSettlementTotalsAsync(Marketplace.MarketplaceId, request, filter, cancellationToken);
        Settlements = await reporting.GetSettlementsAsync(Marketplace.MarketplaceId, request, filter, cancellationToken);
    }

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(SettlementSort column) =>
        LinkWith(("sort", column == SettlementSort.PeriodEnd ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(SettlementSort column) => SettlementFormat.Arrow(Sort == column, Sort.IsDescendingByDefault() != Descending);
}
