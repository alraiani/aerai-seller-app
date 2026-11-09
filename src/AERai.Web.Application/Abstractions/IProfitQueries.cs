using AERai.Web.Application.Profit;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Reads for profit &amp; loss (<c>rpt.vw_ProfitLine</c>, cost history, ad spend, settlements). Rows only: every rule
/// lives in <see cref="ProfitService"/>. Days are the marketplace's local days.
/// </summary>
public interface IProfitQueries
{
    /// <summary>Profit amounts posted on the local days <paramref name="from"/>–<paramref name="to"/>, by day, SKU, category, and held state.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="from">First local day.</param>
    /// <param name="to">Last local day (inclusive).</param>
    /// <param name="timeZoneId">The marketplace's IANA time zone.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<ProfitAmountRow>> GetAmountsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken);

    /// <summary>Units sold on those days by day, SKU, and the unit cost in effect on each order's purchase date.</summary>
    /// <param name="marketplaceId">Marketplace (its costs).</param>
    /// <param name="from">First local day.</param>
    /// <param name="to">Last local day (inclusive).</param>
    /// <param name="timeZoneId">The marketplace's IANA time zone.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<ProfitSoldUnitsRow>> GetSoldUnitsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken);

    /// <summary>Refunded principals posted on those days, with their order item and its cost.</summary>
    /// <param name="marketplaceId">Marketplace (its costs).</param>
    /// <param name="from">First local day.</param>
    /// <param name="to">Last local day (inclusive).</param>
    /// <param name="timeZoneId">The marketplace's IANA time zone.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<ProfitRefundRow>> GetRefundsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, CancellationToken cancellationToken);

    /// <summary>Ads API spend per SKU and day on those days.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<ProfitAdSpendRow>> GetAdSpendAsync(string marketplaceId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>The first day with synced Ads API spend, or <see langword="null"/> when none has synced.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The day.</returns>
    Task<DateOnly?> GetFirstAdSpendDayAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>When the marketplace's ledger takes over from settlement lines, or <see langword="null"/> without a ledger.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The instant (a UTC midnight).</returns>
    Task<DateTimeOffset?> GetLedgerStartAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Catalog details of SKUs.</summary>
    /// <param name="skus">Seller SKUs.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows, for SKUs in the catalog.</returns>
    Task<IReadOnlyList<ProfitProductRow>> GetProductsAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken);

    /// <summary>
    /// The settlements in the marketplace's currency in its settlements list whose period ends between two
    /// instants, each with what the marketplace's ledger has for it.
    /// </summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="periodEndFrom">Period ends on or after this.</param>
    /// <param name="periodEndBefore">Period ends before this.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows, newest first.</returns>
    Task<IReadOnlyList<SettlementTieRow>> GetSettlementTiesAsync(string marketplaceId, DateTimeOffset periodEndFrom, DateTimeOffset periodEndBefore, CancellationToken cancellationToken);

    /// <summary>Ledger amounts on those days that the category rule counted as other expenses, biggest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="from">First local day.</param>
    /// <param name="to">Last local day (inclusive).</param>
    /// <param name="timeZoneId">The marketplace's IANA time zone.</param>
    /// <param name="take">Most kinds to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<ProfitUnmappedRow>> GetOtherExpenseKindsAsync(string marketplaceId, DateOnly from, DateOnly to, string timeZoneId, int take, CancellationToken cancellationToken);
}
