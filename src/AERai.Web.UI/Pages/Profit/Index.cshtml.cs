using System.Globalization;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Profit;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Profit;

/// <summary>
/// Profit &amp; loss by the day Amazon posted each amount: tiles, a chart by month (or week), one collapsible group per
/// product family with a row per SKU, the same figures by month, and a reconciliation view that ties the ledger to
/// each settlement. Everyone can view; filterable by family, searchable, and sortable within each family.
/// </summary>
/// <param name="profit">Profit &amp; loss reports.</param>
/// <param name="families">Product families, for the family filter.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="clock">Clock for the default range.</param>
public sealed class IndexModel(IProfitService profit, IProductFamilyService families, ICurrentMarketplace currentMarketplace, TimeProvider clock) : ListPageModel
{
    /// <summary>The range quick picks, by key.</summary>
    public static readonly IReadOnlyList<(string Key, string Label)> Presets =
        [("month", "This month"), ("last-month", "Last month"), ("3m", "3 months"), ("6m", "6 months"), ("12m", "12 months"), ("ytd", "Year to date")];

    /// <summary>The quick pick used when no range is chosen.</summary>
    public const string DefaultPreset = "3m";

    /// <summary>First local day, from <c>?from=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    /// <summary>Last local day, from <c>?to=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    /// <summary>Only this family (<c>0</c> = no family), from <c>?family=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>Which SKUs, from <c>?show=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "show")]
    public ProfitShow Show { get; set; }

    /// <summary>Sort within each family, from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public ProfitSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>Spread account-level amounts across SKUs, from <c>?loaded=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "loaded")]
    public bool FullyLoaded { get; set; }

    /// <summary>Products, Months, or Reconciliation, from <c>?view=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "view")]
    public ProfitView View { get; set; }

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Today in the marketplace's time zone (the latest day offered).</summary>
    public DateOnly Today { get; private set; }

    /// <summary>Families for the filter.</summary>
    public IReadOnlyList<FamilySummary> Families { get; private set; } = [];

    /// <summary>The report.</summary>
    public ProfitReport Report { get; private set; } = default!;

    /// <summary>The reconciliation, on its view only.</summary>
    public ProfitReconciliation? Reconciliation { get; private set; }

    /// <summary>Whether a search, family, or show filter is applied (the range always is).</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || FamilyId is not null || Show != ProfitShow.All;

    /// <summary>Loads the report.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or just its section for a partial reload.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Today = LocalTime.DateOf(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId));
        Families = await families.ListAsync(cancellationToken);

        var (defaultFrom, defaultTo) = PresetRange(DefaultPreset);
        var to = To ?? defaultTo;
        var from = From ?? (To is null ? defaultFrom : new DateOnly(to.Year, to.Month, 1));
        Report = await profit.GetReportAsync(Marketplace, new ProfitFilter(from, to, FamilyId, Search, Show, Sort, Descending, FullyLoaded), cancellationToken);
        if (View == ProfitView.Reconciliation)
        {
            Reconciliation = await profit.GetReconciliationAsync(Marketplace, Report.From, Report.To, cancellationToken);
        }

        return this.PageOrFragment("_ProfitBody");
    }

    /// <summary>The days a quick pick covers, ending today (or at the end of last month).</summary>
    /// <param name="key">The pick's key.</param>
    /// <returns>Its first and last day.</returns>
    public (DateOnly From, DateOnly To) PresetRange(string key)
    {
        var thisMonth = new DateOnly(Today.Year, Today.Month, 1);
        return key switch
        {
            "month" => (thisMonth, Today),
            "last-month" => (thisMonth.AddMonths(-1), thisMonth.AddDays(-1)),
            "6m" => (thisMonth.AddMonths(-5), Today),
            "12m" => (thisMonth.AddMonths(-11), Today),
            "ytd" => (new DateOnly(Today.Year, 1, 1), Today),
            _ => (thisMonth.AddMonths(-2), Today),
        };
    }

    /// <summary>Link to a quick pick, keeping the other filters (the default clears the range from the address).</summary>
    /// <param name="key">The pick's key.</param>
    /// <returns>A relative URL.</returns>
    public string PresetLink(string key)
    {
        if (key == DefaultPreset)
        {
            return LinkWith(("from", null), ("to", null));
        }

        var (from, to) = PresetRange(key);
        return LinkWith(("from", Format(from)), ("to", Format(to)));
    }

    /// <summary>Whether the range shown is a quick pick's.</summary>
    /// <param name="key">The pick's key.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsPreset(string key) => PresetRange(key) == (Report.From, Report.To);

    /// <summary>Link to a view, keeping the filters.</summary>
    /// <param name="view">The view.</param>
    /// <returns>A relative URL.</returns>
    public string ViewLink(ProfitView view) => LinkWith(("view", view == ProfitView.Products ? null : view.ToString()));

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(ProfitSort column) =>
        LinkWith(("sort", column == ProfitSort.Profit ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null), ("view", null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(ProfitSort column) => SettlementFormat.Arrow(Sort == column, Sort.IsDescendingByDefault() != Descending);

    /// <summary>Link to a SKU's profit over the same range.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <returns>A relative URL.</returns>
    public string SkuLink(string sku) =>
        Url.Page("/Profit/Sku", new { sku, from = Format(Report.From), to = Format(Report.To), loaded = FullyLoaded ? "true" : null }) ?? "#";

    /// <summary>A date as the date inputs and links write it.</summary>
    /// <param name="day">Day.</param>
    /// <returns><c>yyyy-MM-dd</c>.</returns>
    public static string Format(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
