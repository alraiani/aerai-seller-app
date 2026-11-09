using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Profit;

/// <summary>
/// Default <see cref="IProfitService"/>: every profit &amp; loss rule. <see cref="IProfitQueries"/> only returns rows.
/// </summary>
/// <remarks>
/// <para>
/// Amounts count on the local day Amazon posted them (<c>rpt.vw_ProfitLine</c>: the ledger, with settlement lines
/// before it). Sales, refunds, fees, and other expenses come from Amazon as they are.
/// </para>
/// <para>
/// <b>Advertising</b> is Ads API spend per SKU and day from the first day spend was synced; ad charges Amazon billed
/// on those days are left out (they pay for that same spend, later). Before that day, billed charges are spread across
/// each period's SKUs by sales, so older months still carry their ads.
/// </para>
/// <para>
/// <b>Cost of goods</b> charges each unit sold the cost in effect on its order's purchase date (cost history), and
/// gives it back for each unit refunded: the refund's principal ÷ the order item's price each, capped at its
/// quantity (the SKU's average sale price when the order isn't known). Refunded units are taken to go back to stock.
/// </para>
/// <para>
/// With <b>fully loaded</b>, amounts not tied to a SKU (subscriptions, storage and other account fees) are spread
/// across each period's SKUs by sales; otherwise they stay one row. Every split is to the cent
/// (<see cref="ProfitAllocator"/>), so totals never change.
/// </para>
/// </remarks>
/// <param name="queries">Profit reads.</param>
public sealed class ProfitService(IProfitQueries queries) : IProfitService
{
    /// <summary>Most days one report covers.</summary>
    public const int MaxDays = 366;

    /// <summary>Ranges up to this many days are charted by week; longer ones by month.</summary>
    public const int WeeklyUpToDays = 62;

    /// <summary>Most kinds of other expenses listed on the reconciliation view.</summary>
    public const int MaxOtherExpenseKinds = 25;

    /// <inheritdoc/>
    public async Task<ProfitReport> GetReportAsync(Marketplace marketplace, ProfitFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(filter);

        var data = await LoadAsync(marketplace, filter.From, filter.To, filter.FullyLoaded, cancellationToken).ConfigureAwait(false);
        var terms = ProductSearch.Terms(filter.Search);
        var rows = data.SkuRows()
            .Where(r => Selected(r, filter, terms))
            .ToList();

        var groups = rows
            .GroupBy(r => (r.Product.FamilyId, r.Product.Family))
            .OrderBy(g => g.Key.FamilyId is null ? 1 : 0)
            .ThenBy(g => g.Key.Family, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var skus = Sorted(g, filter).ToList();
                return new ProfitFamilyGroup(g.Key.FamilyId, g.Key.Family, skus, ProfitMetrics.Sum(skus.Select(s => s.Metrics)));
            })
            .ToList();

        var unassigned = filter.NarrowsSkus ? [] : data.UnassignedRows();
        var listed = rows.Select(r => r.Product.Sku).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var periods = data.Periods
            .Select(p => new ProfitPeriod(p, data.PeriodMetrics(p, listed, includeUnassigned: !filter.NarrowsSkus)))
            .ToList();

        return new ProfitReport(
            data.From,
            data.To,
            marketplace.Currency,
            groups,
            unassigned,
            ProfitMetrics.Sum(groups.Select(g => g.Totals).Concat(unassigned.Select(u => u.Metrics))),
            periods,
            data.Granularity,
            data.Coverage,
            rows.Count(r => r.Metrics.CostMissingUnits > 0));
    }

    /// <inheritdoc/>
    public async Task<ProfitSkuReport?> GetSkuAsync(Marketplace marketplace, string sku, DateOnly from, DateOnly to, bool fullyLoaded, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var data = await LoadAsync(marketplace, from, to, fullyLoaded, cancellationToken).ConfigureAwait(false);
        var row = data.SkuRows().FirstOrDefault(r => string.Equals(r.Product.Sku, sku, StringComparison.OrdinalIgnoreCase));
        if (row is null)
        {
            return null;
        }

        var only = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { row.Product.Sku };
        var periods = data.Periods.Select(p => new ProfitPeriod(p, data.PeriodMetrics(p, only, includeUnassigned: false))).ToList();
        return new ProfitSkuReport(data.From, data.To, marketplace.Currency, row, periods, data.Granularity, data.Coverage);
    }

    /// <inheritdoc/>
    public async Task<ProfitReconciliation> GetReconciliationAsync(Marketplace marketplace, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var data = await LoadAsync(marketplace, from, to, fullyLoaded: false, cancellationToken).ConfigureAwait(false);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(marketplace.TimeZoneId);
        var ties = await queries.GetSettlementTiesAsync(
            marketplace.MarketplaceId, LocalTime.StartOfDay(data.From, zone), LocalTime.StartOfDay(data.To.AddDays(1), zone), cancellationToken).ConfigureAwait(false);
        var ledgerStart = await queries.GetLedgerStartAsync(marketplace.MarketplaceId, cancellationToken).ConfigureAwait(false);
        var kinds = await queries.GetOtherExpenseKindsAsync(marketplace.MarketplaceId, data.From, data.To, marketplace.TimeZoneId, MaxOtherExpenseKinds, cancellationToken).ConfigureAwait(false);

        var settlements = ties.Select(t => Tie(t, ledgerStart)).ToList();
        var totals = data.AllMetrics();
        var payouts = ties.Sum(t => t.Net);
        var reserve = ties.Sum(t => t.Reserve);

        // What Amazon posted in the range, before profit swaps billed ad charges for spend and adds cost of goods.
        var posted = totals.Sales + totals.Refunds + totals.AmazonFees + totals.Other + data.BilledAdvertising;
        var bridge = new ProfitBridge(
            payouts,
            reserve,
            Timing: posted - (payouts - reserve),
            AdvertisingAdjustment: totals.Advertising - data.BilledAdvertising,
            totals.Cogs,
            totals.Profit);

        return new ProfitReconciliation(settlements, bridge, kinds, data.Coverage.LedgerStartsOn);
    }

    /// <summary>A settlement against the ledger: tied to the cent, different, or older than the ledger.</summary>
    private static SettlementTie Tie(SettlementTieRow row, DateTimeOffset? ledgerStart)
    {
        if (ledgerStart is not { } starts || row.PeriodStart is not { } periodStart || periodStart < starts)
        {
            return new SettlementTie(row, SettlementTieState.BeforeLedger, null);
        }

        var difference = row.Net - (row.LedgerTotal ?? 0m);
        return new SettlementTie(row, difference == 0 ? SettlementTieState.Ties : SettlementTieState.Differs, difference);
    }

    /// <summary>Reads a range and applies every rule, by period and SKU.</summary>
    private async Task<ProfitData> LoadAsync(Marketplace marketplace, DateOnly from, DateOnly to, bool fullyLoaded, CancellationToken cancellationToken)
    {
        (from, to) = Range(from, to);
        var id = marketplace.MarketplaceId;
        var zone = marketplace.TimeZoneId;
        var amounts = await queries.GetAmountsAsync(id, from, to, zone, cancellationToken).ConfigureAwait(false);
        var sold = await queries.GetSoldUnitsAsync(id, from, to, zone, cancellationToken).ConfigureAwait(false);
        var refunds = await queries.GetRefundsAsync(id, from, to, zone, cancellationToken).ConfigureAwait(false);
        var spend = await queries.GetAdSpendAsync(id, from, to, cancellationToken).ConfigureAwait(false);
        var firstSpend = await queries.GetFirstAdSpendDayAsync(id, cancellationToken).ConfigureAwait(false);
        var ledgerStart = await queries.GetLedgerStartAsync(id, cancellationToken).ConfigureAwait(false);

        var data = new ProfitData(from, to, firstSpend, ledgerStart is { } starts ? LocalTime.DateOf(starts, TimeZoneInfo.FindSystemTimeZoneById(zone)) : null);
        data.Add(amounts, sold, spend);
        data.AddRefunds(refunds);
        data.SpreadBilledAdvertising();
        if (fullyLoaded)
        {
            data.SpreadAccountAmounts();
        }

        var skus = data.Skus.ToList();
        var products = skus.Count == 0 ? [] : await queries.GetProductsAsync(skus, cancellationToken).ConfigureAwait(false);
        data.SetProducts(products);
        return data;
    }

    /// <summary>The range in order and at most <see cref="MaxDays"/> long, keeping its latest days.</summary>
    /// <param name="from">First day asked for.</param>
    /// <param name="to">Last day asked for.</param>
    /// <returns>The range the report covers.</returns>
    public static (DateOnly From, DateOnly To) Range(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            (from, to) = (to, from);
        }

        return to.DayNumber - from.DayNumber + 1 > MaxDays ? (to.AddDays(-(MaxDays - 1)), to) : (from, to);
    }

    private static bool Selected(ProfitSkuRow row, ProfitFilter filter, IReadOnlyList<string> terms)
    {
        var product = row.Product;
        if (filter.FamilyId is { } family && (family == ProfitFilter.NoFamily ? product.FamilyId is not null : product.FamilyId != family))
        {
            return false;
        }

        if (filter.Show == ProfitShow.LosingMoney && row.Metrics.Profit >= 0 || filter.Show == ProfitShow.CostMissing && row.Metrics.CostMissingUnits == 0)
        {
            return false;
        }

        return terms.All(term =>
            product.Sku.Contains(term, StringComparison.OrdinalIgnoreCase)
            || (product.Asin?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (product.Title?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
            || (product.Family?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static IEnumerable<ProfitSkuRow> Sorted(IEnumerable<ProfitSkuRow> rows, ProfitFilter filter)
    {
        var descending = filter.Sort.IsDescendingByDefault() != filter.Descending;
        if (filter.Sort == ProfitSort.Sku)
        {
            return descending
                ? rows.OrderByDescending(r => r.Product.Sku, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(r => r.Product.Sku, StringComparer.OrdinalIgnoreCase);
        }

        // A SKU without a margin (no sales) goes last either way.
        Func<ProfitSkuRow, decimal?> key = filter.Sort switch
        {
            ProfitSort.Units => r => r.Metrics.Units,
            ProfitSort.Sales => r => r.Metrics.Sales,
            ProfitSort.Refunds => r => r.Metrics.Refunds,
            ProfitSort.AmazonFees => r => r.Metrics.AmazonFees,
            ProfitSort.Other => r => r.Metrics.Other,
            ProfitSort.Advertising => r => r.Metrics.Advertising,
            ProfitSort.Cogs => r => r.Metrics.Cogs,
            ProfitSort.Margin => r => r.Metrics.Margin,
            _ => r => r.Metrics.Profit,
        };
        var known = rows.OrderBy(r => key(r) is null ? 1 : 0);
        return (descending ? known.ThenByDescending(key) : known.ThenBy(key))
            .ThenBy(r => r.Product.Sku, StringComparer.OrdinalIgnoreCase);
    }
}
