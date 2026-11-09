namespace AERai.Web.Application.Profit;

/// <summary>Profit &amp; loss for a range of local days, as the page shows it.</summary>
/// <param name="From">First day.</param>
/// <param name="To">Last day (inclusive).</param>
/// <param name="Currency">The marketplace's currency (every amount is in it).</param>
/// <param name="Families">The listed SKUs, one group per family (<em>No family</em> last).</param>
/// <param name="Unassigned">Amounts not tied to a SKU, when the filter lists every SKU and they weren't spread.</param>
/// <param name="Totals">The listed SKUs plus <paramref name="Unassigned"/>.</param>
/// <param name="Periods">The same figures by month (or by week for short ranges), oldest first.</param>
/// <param name="Granularity">Whether <paramref name="Periods"/> are months or weeks.</param>
/// <param name="Coverage">Where the figures come from.</param>
/// <param name="CostMissingSkus">Listed SKUs with units sold and no cost on their order date.</param>
public sealed record ProfitReport(
    DateOnly From,
    DateOnly To,
    string Currency,
    IReadOnlyList<ProfitFamilyGroup> Families,
    IReadOnlyList<ProfitUnassignedRow> Unassigned,
    ProfitMetrics Totals,
    IReadOnlyList<ProfitPeriod> Periods,
    ProfitGranularity Granularity,
    ProfitCoverage Coverage,
    int CostMissingSkus)
{
    /// <summary>Every listed SKU, in family order.</summary>
    public IEnumerable<ProfitSkuRow> Skus => Families.SelectMany(f => f.Skus);
}

/// <summary>One family's SKUs on the profit page.</summary>
/// <param name="FamilyId">The family, or <see langword="null"/> for SKUs without one.</param>
/// <param name="Family">Its name.</param>
/// <param name="Skus">Its SKUs, sorted.</param>
/// <param name="Totals">Its SKUs added up.</param>
public sealed record ProfitFamilyGroup(int? FamilyId, string? Family, IReadOnlyList<ProfitSkuRow> Skus, ProfitMetrics Totals);

/// <summary>One SKU's profit.</summary>
/// <param name="Product">Its catalog details.</param>
/// <param name="Metrics">Its figures.</param>
/// <param name="BilledAdvertising">Whether part of its advertising is billed charges spread by sales (days before spend synced).</param>
public sealed record ProfitSkuRow(ProfitProductRow Product, ProfitMetrics Metrics, bool BilledAdvertising);

/// <summary>Amounts not tied to a SKU.</summary>
/// <param name="Kind">What they are.</param>
/// <param name="Metrics">Their figures.</param>
public sealed record ProfitUnassignedRow(ProfitUnassignedKind Kind, ProfitMetrics Metrics);

/// <summary>What a row of amounts not tied to a SKU holds.</summary>
public enum ProfitUnassignedKind
{
    /// <summary>Account-level amounts: subscriptions, storage and other fees, reimbursements without a SKU.</summary>
    AccountAmounts = 1,

    /// <summary>Billed ad charges in a period with no sales to spread them by.</summary>
    AdvertisingNotSplit = 2,
}

/// <summary>A month or week of profit.</summary>
/// <param name="Start">Its first day.</param>
/// <param name="Metrics">Its figures (listed SKUs, plus amounts not tied to a SKU when every SKU is listed).</param>
public sealed record ProfitPeriod(DateOnly Start, ProfitMetrics Metrics);

/// <summary>Whether profit periods are months or weeks.</summary>
public enum ProfitGranularity
{
    /// <summary>Calendar months.</summary>
    Month = 1,

    /// <summary>Weeks starting Monday.</summary>
    Week = 2,
}

/// <summary>Where a report's figures come from.</summary>
/// <param name="LedgerStartsOn">The first local day read from the ledger, or <see langword="null"/> without one; days before are settlement lines.</param>
/// <param name="AdSpendStartsOn">The first day with Ads API spend; days before use billed ad charges.</param>
public sealed record ProfitCoverage(DateOnly? LedgerStartsOn, DateOnly? AdSpendStartsOn);
