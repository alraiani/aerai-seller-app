using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Security;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// Settlement payouts (from <c>rpt.vw_SettlementSummary</c>): totals for what's listed, then the
/// settlements, newest period first. Searchable by settlement, order, SKU, ASIN, or title; filterable
/// by period end and the currency paid in; sortable by period and each amount column.
/// </summary>
/// <remarks>
/// Amazon lists every North America payout with the United States, so its list holds CAD and MXN
/// settlements too. Amounts and totals are in the marketplace's currency (converted at each settlement's
/// rate); each row is tagged with where it came from and keeps its original net beside the converted one.
/// </remarks>
/// <param name="reporting">Reporting queries.</param>
/// <param name="deferred">What Amazon still holds back (the deferred tile).</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IReportingQueries reporting, IDeferredPaymentService deferred, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Only periods ending on or after this local date, from <c>?from=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    /// <summary>Only periods ending on or before this local date, from <c>?to=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    /// <summary>Only settlements paid in this currency, from <c>?currency=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "currency")]
    public string? Currency { get; set; }

    /// <summary>Sort from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public SettlementSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The marketplace's time zone, in which periods are shown.</summary>
    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>What Amazon holds back for the marketplace and has not paid out yet.</summary>
    public DeferredOverview Deferred { get; private set; } = new([], new DeferredSyncState(false, false, null), DateTimeOffset.MinValue);

    /// <summary>What Amazon deferred out of each listed settlement's payout, by settlement id, in the marketplace's currency.</summary>
    public IReadOnlyDictionary<string, DeferredOut> DeferredOut { get; private set; } = new Dictionary<string, DeferredOut>();

    /// <summary>The current page of settlements.</summary>
    public PagedResult<SettlementSummary> Settlements { get; private set; } = default!;

    /// <summary>
    /// Totals for the tiles, in the marketplace's currency, over every settlement the search and filters
    /// select (those still waiting for an exchange rate are counted apart).
    /// </summary>
    public SettlementTotals Totals { get; private set; } = SettlementTotals.Empty(string.Empty);

    /// <summary>
    /// The marketplace whose list holds this marketplace's payouts (Canada's are with the United States),
    /// or <see langword="null"/> when it keeps its own.
    /// </summary>
    public Domain.Core.Marketplace? ListedUnder { get; private set; }

    /// <summary>Whether the user may fetch exchange rates.</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Every currency with matching settlements, ignoring the currency filter (for its choices).</summary>
    public IReadOnlyList<string> Currencies { get; private set; } = [];

    /// <summary>Whether a search, date, or currency filter is applied.</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || From is not null || To is not null || Currency is not null;

    /// <summary>Loads the totals and the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or just its section for a partial reload.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var selection = await currentMarketplace.GetAsync(cancellationToken);
        Marketplace = selection.Current;
        ListedUnder = Marketplace.SettlementMarketplaceId is { } listedUnder ? selection.All.FirstOrDefault(m => m.MarketplaceId == listedUnder) : null;

        // Dates are the marketplace's local days, like every other date filter in the app.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);
        Zone = zone;
        var filter = new SettlementListFilter(
            From is { } from ? LocalTime.StartOfDay(from, zone) : null,
            To is { } to ? LocalTime.StartOfDay(to.AddDays(1), zone) : null,
            string.IsNullOrWhiteSpace(Currency) ? null : Currency.Trim(),
            Sort,
            Descending);
        var request = ToPageRequest();

        Totals = await reporting.GetSettlementTotalsAsync(Marketplace.MarketplaceId, request, filter, cancellationToken)
            ?? SettlementTotals.Empty(Marketplace.Currency);
        Currencies = await reporting.GetSettlementCurrenciesAsync(Marketplace.MarketplaceId, request, filter, cancellationToken);

        Settlements = await reporting.GetSettlementsAsync(Marketplace.MarketplaceId, request, filter, cancellationToken);
        Deferred = await deferred.GetOverviewAsync(Marketplace, cancellationToken);
        DeferredOut = await deferred.GetDeferredOutAsync(Marketplace, Settlements.Items, SettlementAmounts.Home, cancellationToken);
        return this.PageOrFragment("_SettlementsBody");
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
