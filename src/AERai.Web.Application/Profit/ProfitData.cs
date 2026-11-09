using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Profit;

/// <summary>
/// One range's profit figures by period and SKU while <see cref="ProfitService"/> applies its rules: categories into
/// columns, ad spend in place of billed charges, cost of goods, refunded units, and the spreads by sales.
/// </summary>
/// <remarks>
/// SKUs are matched ignoring case (SQL Server compares them so), each kept in the spelling first seen. Amounts not
/// tied to a SKU are kept apart per period until (and unless) they are spread.
/// </remarks>
internal sealed class ProfitData
{
    private readonly Dictionary<(DateOnly Period, string Sku), Cell> _cells = [];
    private readonly Dictionary<DateOnly, Cell> _account = [];
    private readonly Dictionary<DateOnly, decimal> _billedToSpread = [];
    private readonly Dictionary<DateOnly, decimal> _adsNotSplit = [];
    private readonly Dictionary<string, string> _spelling = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _billedSkus = new(StringComparer.OrdinalIgnoreCase);
    private readonly DateOnly? _firstSpendDay;
    private Dictionary<string, ProfitProductRow> _products = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Creates the figures for a range.</summary>
    /// <param name="from">First local day.</param>
    /// <param name="to">Last local day (inclusive).</param>
    /// <param name="firstSpendDay">The first day with Ads API spend.</param>
    /// <param name="ledgerStartsOn">The first local day read from the ledger.</param>
    public ProfitData(DateOnly from, DateOnly to, DateOnly? firstSpendDay, DateOnly? ledgerStartsOn)
    {
        From = from;
        To = to;
        _firstSpendDay = firstSpendDay;
        Coverage = new ProfitCoverage(ledgerStartsOn, firstSpendDay);
        Granularity = to.DayNumber - from.DayNumber + 1 <= ProfitService.WeeklyUpToDays ? ProfitGranularity.Week : ProfitGranularity.Month;

        var periods = new List<DateOnly>();
        for (var p = PeriodOf(from); p <= to; p = Granularity == ProfitGranularity.Month ? p.AddMonths(1) : p.AddDays(7))
        {
            periods.Add(p);
        }

        Periods = periods;
    }

    /// <summary>First day.</summary>
    public DateOnly From { get; }

    /// <summary>Last day (inclusive).</summary>
    public DateOnly To { get; }

    /// <summary>Months or weeks.</summary>
    public ProfitGranularity Granularity { get; }

    /// <summary>Every period of the range, oldest first.</summary>
    public IReadOnlyList<DateOnly> Periods { get; }

    /// <summary>Where the figures come from.</summary>
    public ProfitCoverage Coverage { get; }

    /// <summary>Every ad charge Amazon billed in the range (negative), including those spend replaced.</summary>
    public decimal BilledAdvertising { get; private set; }

    /// <summary>Every SKU with figures.</summary>
    public IEnumerable<string> Skus => _cells.Keys.Select(k => k.Sku).Distinct(StringComparer.OrdinalIgnoreCase);

    /// <summary>The first day of the period a day falls in (a month, or a week starting Monday).</summary>
    /// <param name="day">The day.</param>
    /// <returns>Its period's first day.</returns>
    public DateOnly PeriodOf(DateOnly day) => Granularity == ProfitGranularity.Month
        ? new DateOnly(day.Year, day.Month, 1)
        : day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    /// <summary>Adds the posted amounts, the units sold with their cost, and the ad spend.</summary>
    /// <param name="amounts">Posted amounts.</param>
    /// <param name="sold">Units sold with their unit cost.</param>
    /// <param name="spend">Ads API spend.</param>
    public void Add(IEnumerable<ProfitAmountRow> amounts, IEnumerable<ProfitSoldUnitsRow> sold, IEnumerable<ProfitAdSpendRow> spend)
    {
        foreach (var a in amounts)
        {
            var period = PeriodOf(a.Day);
            switch (a.Category)
            {
                case ProfitCategory.Reserve or ProfitCategory.Transfer:
                    continue;
                case ProfitCategory.Advertising:
                    BilledAdvertising += a.Amount;

                    // From the first day spend synced, spend is the cost; the charges pay for that same spend, later.
                    if (_firstSpendDay is not { } first || a.Day < first)
                    {
                        _billedToSpread[period] = _billedToSpread.GetValueOrDefault(period) + a.Amount;
                    }

                    continue;
            }

            var cell = a.Sku is null ? Account(period) : CellOf(period, a.Sku);
            switch (a.Category)
            {
                case ProfitCategory.Sales:
                    cell.Sales += a.Amount;
                    break;
                case ProfitCategory.RefundedSales or ProfitCategory.RefundedExpenses:
                    cell.Refunds += a.Amount;
                    break;
                case ProfitCategory.Shipping or ProfitCategory.Commission or ProfitCategory.FbaFees:
                    cell.AmazonFees += a.Amount;
                    break;
                default:
                    cell.Other += a.Amount;
                    break;
            }

            if (a.Held)
            {
                cell.Held += a.Amount;
            }
        }

        foreach (var s in sold)
        {
            var cell = CellOf(PeriodOf(s.Day), s.Sku);
            cell.Units += s.Units;
            if (s.UnitCost is { } cost)
            {
                cell.Cogs -= s.Units * cost;
            }
            else
            {
                cell.CostMissingUnits += s.Units;
            }
        }

        foreach (var s in spend)
        {
            CellOf(PeriodOf(s.Day), s.Sku).Advertising -= s.Spend;
        }
    }

    /// <summary>Gives back the cost of refunded units (call after <see cref="Add"/>, which sets the fallback prices).</summary>
    /// <param name="refunds">Refunded principals with their order items.</param>
    public void AddRefunds(IEnumerable<ProfitRefundRow> refunds)
    {
        // When the order isn't known, a refund is counted in units at the SKU's average sale price over the range.
        var averagePrice = _cells
            .GroupBy(c => c.Key.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Sku: g.Key, Sales: g.Sum(c => c.Value.Sales), Units: g.Sum(c => c.Value.Units)))
            .Where(s => s.Units > 0 && s.Sales > 0)
            .ToDictionary(s => s.Sku, s => s.Sales / s.Units, StringComparer.OrdinalIgnoreCase);

        foreach (var r in refunds)
        {
            var priceEach = r is { ItemPrice: > 0, ItemQuantity: > 0 } ? r.ItemPrice!.Value / r.ItemQuantity!.Value : averagePrice.GetValueOrDefault(r.Sku);
            if (priceEach <= 0 || r.RefundedPrincipal >= 0)
            {
                continue;
            }

            var units = (int)decimal.Round(-r.RefundedPrincipal / priceEach, 0, MidpointRounding.AwayFromZero);
            if (r.ItemQuantity is > 0 and var quantity)
            {
                units = Math.Min(units, quantity);
            }

            if (units <= 0)
            {
                continue;
            }

            var cell = CellOf(PeriodOf(r.Day), r.Sku);
            cell.RefundedUnits += units;
            if (r.UnitCost is { } cost)
            {
                cell.CostReturned += units * cost;
                cell.Cogs += units * cost;
            }
        }
    }

    /// <summary>Spreads each period's billed ad charges (days before spend synced) across its SKUs by sales.</summary>
    public void SpreadBilledAdvertising()
    {
        foreach (var (period, amount) in _billedToSpread)
        {
            var cells = SellingCells(period);
            if (ProfitAllocator.Split(amount, [.. cells.Select(c => c.Value.Sales)]) is not { } parts)
            {
                _adsNotSplit[period] = _adsNotSplit.GetValueOrDefault(period) + amount;
                continue;
            }

            for (var i = 0; i < cells.Count; i++)
            {
                cells[i].Value.Advertising += parts[i];
                _billedSkus.Add(cells[i].Key.Sku);
            }
        }
    }

    /// <summary>Spreads each period's amounts not tied to a SKU across its SKUs by sales (fully loaded).</summary>
    public void SpreadAccountAmounts()
    {
        foreach (var (period, account) in _account)
        {
            var cells = SellingCells(period);
            var weights = cells.Select(c => c.Value.Sales).ToList();
            if (ProfitAllocator.Split(0m, weights) is null)
            {
                continue;
            }

            void Spread(decimal amount, Action<Cell, decimal> add)
            {
                var parts = ProfitAllocator.Split(amount, weights)!; // Non-null: the weights were checked just above.
                for (var i = 0; i < cells.Count; i++)
                {
                    add(cells[i].Value, parts[i]);
                }
            }

            Spread(account.Sales, (c, v) => c.Sales += v);
            Spread(account.Refunds, (c, v) => c.Refunds += v);
            Spread(account.AmazonFees, (c, v) => c.AmazonFees += v);
            Spread(account.Other, (c, v) => c.Other += v);
            Spread(account.Held, (c, v) => c.Held += v);
            _account[period] = new Cell();
        }
    }

    /// <summary>Sets the SKUs' catalog details.</summary>
    /// <param name="products">The details.</param>
    public void SetProducts(IEnumerable<ProfitProductRow> products) =>
        _products = products.GroupBy(p => p.Sku, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    /// <summary>Each SKU with figures, over the whole range.</summary>
    /// <returns>The rows, unordered.</returns>
    public IEnumerable<ProfitSkuRow> SkuRows() => _cells
        .GroupBy(c => c.Key.Sku, StringComparer.OrdinalIgnoreCase)
        .Select(g => new ProfitSkuRow(
            _products.GetValueOrDefault(g.Key) ?? new ProfitProductRow(g.Key, null, null, null, null, null),
            ProfitMetrics.Sum(g.Select(c => c.Value.ToMetrics())),
            _billedSkus.Contains(g.Key)))
        .Where(r => !r.Metrics.IsEmpty);

    /// <summary>The amounts not tied to a SKU, over the whole range.</summary>
    /// <returns>Up to two rows: account-level amounts and billed ads with no sales to spread them by.</returns>
    public IReadOnlyList<ProfitUnassignedRow> UnassignedRows()
    {
        var rows = new List<ProfitUnassignedRow>();
        var account = ProfitMetrics.Sum(_account.Values.Select(c => c.ToMetrics()));
        if (!account.IsEmpty)
        {
            rows.Add(new ProfitUnassignedRow(ProfitUnassignedKind.AccountAmounts, account));
        }

        var ads = _adsNotSplit.Values.Sum();
        if (ads != 0)
        {
            rows.Add(new ProfitUnassignedRow(ProfitUnassignedKind.AdvertisingNotSplit, ProfitMetrics.Zero with { Advertising = ads }));
        }

        return rows;
    }

    /// <summary>One period's figures for some SKUs.</summary>
    /// <param name="period">The period's first day.</param>
    /// <param name="skus">The SKUs to add up.</param>
    /// <param name="includeUnassigned">Whether amounts not tied to a SKU are added too.</param>
    /// <returns>The figures.</returns>
    public ProfitMetrics PeriodMetrics(DateOnly period, IReadOnlySet<string> skus, bool includeUnassigned)
    {
        var total = ProfitMetrics.Sum(_cells.Where(c => c.Key.Period == period && skus.Contains(c.Key.Sku)).Select(c => c.Value.ToMetrics()));
        if (includeUnassigned)
        {
            total += _account.TryGetValue(period, out var account) ? account.ToMetrics() : ProfitMetrics.Zero;
            total += ProfitMetrics.Zero with { Advertising = _adsNotSplit.GetValueOrDefault(period) };
        }

        return total;
    }

    /// <summary>Every figure of the range: all SKUs and everything not tied to one.</summary>
    /// <returns>The figures.</returns>
    public ProfitMetrics AllMetrics() =>
        ProfitMetrics.Sum(_cells.Values.Concat(_account.Values).Select(c => c.ToMetrics()))
        + (ProfitMetrics.Zero with { Advertising = _adsNotSplit.Values.Sum() });

    private List<KeyValuePair<(DateOnly Period, string Sku), Cell>> SellingCells(DateOnly period) =>
        [.. _cells.Where(c => c.Key.Period == period && c.Value.Sales > 0).OrderBy(c => c.Key.Sku, StringComparer.Ordinal)];

    private Cell CellOf(DateOnly period, string sku)
    {
        if (!_spelling.TryGetValue(sku, out var spelled))
        {
            spelled = sku;
            _spelling[sku] = sku;
        }

        var key = (period, spelled);
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new Cell();
            _cells[key] = cell;
        }

        return cell;
    }

    private Cell Account(DateOnly period)
    {
        if (!_account.TryGetValue(period, out var cell))
        {
            cell = new Cell();
            _account[period] = cell;
        }

        return cell;
    }

    /// <summary>Running figures for one period and SKU.</summary>
    private sealed class Cell
    {
        public int Units { get; set; }

        public int RefundedUnits { get; set; }

        public decimal Sales { get; set; }

        public decimal Refunds { get; set; }

        public decimal AmazonFees { get; set; }

        public decimal Other { get; set; }

        public decimal Advertising { get; set; }

        public decimal Cogs { get; set; }

        public decimal CostReturned { get; set; }

        public int CostMissingUnits { get; set; }

        public decimal Held { get; set; }

        public ProfitMetrics ToMetrics() =>
            new(Units, RefundedUnits, Sales, Refunds, AmazonFees, Other, Advertising, Cogs, CostReturned, CostMissingUnits, Held);
    }
}
