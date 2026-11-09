using System.Globalization;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Profit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Profit;

/// <summary>
/// One SKU's profit &amp; loss over a range: its figures, a chart by month (or week), and the same by period, with
/// links to its orders and settlements. Reached from a SKU on the profit page, keeping its range.
/// </summary>
/// <param name="profit">Profit &amp; loss reports.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
/// <param name="clock">Clock for the default range.</param>
public sealed class SkuModel(IProfitService profit, ICurrentMarketplace currentMarketplace, TimeProvider clock) : PageModel
{
    /// <summary>First local day, from <c>?from=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "from")]
    public DateOnly? From { get; set; }

    /// <summary>Last local day, from <c>?to=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "to")]
    public DateOnly? To { get; set; }

    /// <summary>Spread account-level amounts across SKUs, from <c>?loaded=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "loaded")]
    public bool FullyLoaded { get; set; }

    /// <summary>The SKU asked for, from the route.</summary>
    public string Sku { get; private set; } = string.Empty;

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The report, or <see langword="null"/> when the SKU has nothing in the range.</summary>
    public ProfitSkuReport? Report { get; private set; }

    /// <summary>The range shown (the report's, or the one asked for when there is none).</summary>
    public (DateOnly From, DateOnly To) Range { get; private set; }

    /// <summary>Loads the SKU's report.</summary>
    /// <param name="sku">Seller SKU from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    public async Task<IActionResult> OnGetAsync(string sku, CancellationToken cancellationToken)
    {
        Sku = sku;
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var today = LocalTime.DateOf(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId));
        var to = To ?? today;
        var from = From ?? new DateOnly(to.Year, to.Month, 1).AddMonths(-2);
        Range = ProfitService.Range(from, to);
        Report = await profit.GetSkuAsync(Marketplace, sku, Range.From, Range.To, FullyLoaded, cancellationToken);
        return Page();
    }

    /// <summary>Link back to the profit page over the same range.</summary>
    /// <returns>A relative URL.</returns>
    public string BackLink() =>
        Url.Page("/Profit/Index", new { from = Format(Range.From), to = Format(Range.To), loaded = FullyLoaded ? "true" : null }) ?? "/Profit";

    /// <summary>A date as links write it.</summary>
    /// <param name="day">Day.</param>
    /// <returns><c>yyyy-MM-dd</c>.</returns>
    public static string Format(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
