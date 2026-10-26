using AERai.Web.Application.Advertising;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Advertising;

/// <summary>
/// Advertising: what Sponsored Products ads cost over a range of local days, one collapsible group per
/// product family with its subtotal and a row per advertised SKU, with ACoS and TACoS. Filterable by
/// family and searchable; SKUs sort by spend, sales, ratios, or clicks within each family.
/// </summary>
/// <param name="advertising">Advertising report.</param>
/// <param name="families">Product families, for the family filter.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="clock">Clock for the default range.</param>
public sealed class IndexModel(
    IAdvertisingService advertising, IProductFamilyService families, ICurrentMarketplace currentMarketplace, TimeProvider clock) : ListPageModel
{
    /// <summary>Range lengths offered as quick picks.</summary>
    public static readonly IReadOnlyList<int> PresetDays = [7, 30, 90];

    /// <summary>Days shown when no range is chosen.</summary>
    public const int DefaultDays = 30;

    /// <summary>First local day, from <c>?from=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    /// <summary>Last local day, from <c>?to=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    /// <summary>Only this family (<c>0</c> = no family), from <c>?family=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "family")]
    public int? FamilyId { get; set; }

    /// <summary>Sort within each family, from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public AdSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>
    /// The latest day the page offers: yesterday in the marketplace's time zone, the last day the Ads spend sync
    /// pulls (a partial today would understate TACoS against the day's sales).
    /// </summary>
    public DateOnly LastDay { get; private set; }

    /// <summary>Families for the filter.</summary>
    public IReadOnlyList<FamilySummary> Families { get; private set; } = [];

    /// <summary>The report.</summary>
    public AdvertisingReport Report { get; private set; } = default!;

    /// <summary>Whether a search or family filter is applied (the range always is).</summary>
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Search) || FamilyId is not null;

    /// <summary>Loads the report.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or just its section for a partial reload.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        LastDay = LocalTime.DateOf(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId)).AddDays(-1);
        Families = await families.ListAsync(cancellationToken);

        var to = To ?? LastDay;
        var from = From ?? to.AddDays(-(DefaultDays - 1));
        Report = await advertising.GetReportAsync(Marketplace, new AdvertisingFilter(from, to, FamilyId, Search, Sort, Descending), cancellationToken);
        return this.PageOrFragment("_AdvertisingBody");
    }

    /// <summary>Link to the last <paramref name="days"/> days through yesterday, keeping the other filters.</summary>
    /// <param name="days">Days ending yesterday.</param>
    /// <returns>A relative URL.</returns>
    public string PresetLink(int days) =>
        days == DefaultDays ? LinkWith(("from", null), ("to", null)) : LinkWith(("from", Format(LastDay.AddDays(-(days - 1)))), ("to", null));

    /// <summary>Whether the range shown is the last <paramref name="days"/> days through yesterday.</summary>
    /// <param name="days">Days ending yesterday.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsPreset(int days) => Report.To == LastDay && Report.From == LastDay.AddDays(-(days - 1));

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(AdSort column) =>
        LinkWith(("sort", column == AdSort.Spend ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(AdSort column) => SettlementFormat.Arrow(Sort == column, Sort.IsDescendingByDefault() != Descending);

    /// <summary>A date as the date inputs and links write it.</summary>
    /// <param name="day">Day.</param>
    /// <returns><c>yyyy-MM-dd</c>.</returns>
    public static string Format(DateOnly day) => day.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
