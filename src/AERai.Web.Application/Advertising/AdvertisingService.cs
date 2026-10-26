using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Advertising;

/// <summary>Default <see cref="IAdvertisingService"/>: groups advertised SKUs by family and adds them up.</summary>
/// <param name="queries">Advertising rows.</param>
public sealed class AdvertisingService(IAdvertisingQueries queries) : IAdvertisingService
{
    /// <summary>The longest range the page adds up at once (about a year), so a typo in a date can't scan years of rows.</summary>
    public const int MaxDays = 366;

    /// <inheritdoc/>
    public async Task<AdvertisingReport> GetReportAsync(Marketplace marketplace, AdvertisingFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(filter);

        var (from, to) = Range(filter.From, filter.To);
        var spendThrough = await queries.GetLatestSpendDateAsync(marketplace.MarketplaceId, cancellationToken).ConfigureAwait(false);
        var rows = (await queries.GetSkuSpendAsync(marketplace.MarketplaceId, from, to, cancellationToken).ConfigureAwait(false))
            .Where(r => Selected(r, filter))
            .ToList();

        // TACoS compares spend with every sale of the SKU over the same local days, as the Orders page counts them.
        var zone = TimeZoneInfo.FindSystemTimeZoneById(marketplace.TimeZoneId);
        var sales = rows.Count == 0
            ? new Dictionary<string, (int Units, decimal Sales)>()
            : await queries.GetSalesBySkuAsync(
                marketplace.MarketplaceId, LocalTime.StartOfDay(from, zone), LocalTime.StartOfDay(to.AddDays(1), zone), [.. rows.Select(r => r.Sku)], cancellationToken).ConfigureAwait(false);
        rows = [.. rows.Select(r => sales.TryGetValue(r.Sku, out var s)
            ? r with { Metrics = r.Metrics with { TotalSales = s.Sales, TotalUnits = s.Units } }
            : r)];

        var groups = rows
            .GroupBy(r => (r.FamilyId, r.Family))
            .OrderBy(g => g.Key.FamilyId is null ? 1 : 0)
            .ThenBy(g => g.Key.Family, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var skus = Sorted(g, filter).ToList();
                return new AdFamilyGroup(g.Key.FamilyId, g.Key.Family, skus, AdMetrics.Sum(skus.Select(s => s.Metrics)));
            })
            .ToList();

        return new AdvertisingReport(from, to, groups, AdMetrics.Sum(groups.Select(g => g.Totals)), spendThrough);
    }

    /// <summary>The range in order and at most <see cref="MaxDays"/> long (the latest days are kept).</summary>
    private static (DateOnly From, DateOnly To) Range(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        return to.DayNumber - from.DayNumber >= MaxDays ? (to.AddDays(-(MaxDays - 1)), to) : (from, to);
    }

    private static bool Selected(AdSkuRow row, AdvertisingFilter filter)
    {
        if (filter.FamilyId is { } familyId && (row.FamilyId ?? AdvertisingFilter.NoFamily) != familyId)
        {
            return false;
        }

        // Every word must match something about the SKU, so "towel blue" narrows rather than widens.
        return ProductSearch.Terms(filter.Search).All(term =>
            Contains(row.Sku, term) || Contains(row.Asin, term) || Contains(row.Title, term) || Contains(row.Family, term));
    }

    private static bool Contains(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    /// <summary>SKUs in the chosen order; a SKU whose ratio can't be worked out goes last either way.</summary>
    private static IEnumerable<AdSkuRow> Sorted(IEnumerable<AdSkuRow> rows, AdvertisingFilter filter)
    {
        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        Func<AdSkuRow, decimal?> key = filter.Sort switch
        {
            AdSort.AdSales => r => r.Metrics.AdSales,
            AdSort.Acos => r => r.Metrics.Acos,
            AdSort.Tacos => r => r.Metrics.Tacos,
            AdSort.Clicks => r => r.Metrics.Clicks,
            AdSort.TotalSales => r => r.Metrics.TotalSales,
            _ => r => r.Metrics.Spend,
        };

        var withValue = rows.OrderBy(r => key(r) is null ? 1 : 0);
        var ordered = descending ? withValue.ThenByDescending(key) : withValue.ThenBy(key);
        return ordered.ThenBy(r => r.Sku, StringComparer.OrdinalIgnoreCase);
    }
}
