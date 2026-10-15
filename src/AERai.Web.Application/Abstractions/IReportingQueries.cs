using AERai.Web.Application.Common;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Read-only queries over the <c>rpt</c> reporting views.
/// </summary>
public interface IReportingQueries
{
    /// <summary>Lists orders, newest first.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Paging; search matches order id or status.</param>
    /// <returns>One page of orders.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<OrderSummary>> GetOrdersAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken);

    /// <summary>Lists settlements, newest period first unless the filter sorts otherwise.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Paging; search matches the settlement id, or any SKU, ASIN, title, or order id in the settlement.</param>
    /// <param name="filter">Period range and sort; <see langword="null"/> for <see cref="SettlementListFilter.Default"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of settlements.</returns>
    Task<PagedResult<SettlementSummary>> GetSettlementsAsync(string marketplaceId, PageRequest request, SettlementListFilter? filter, CancellationToken cancellationToken);

    /// <summary>
    /// Totals over every settlement the same search and filter select, across all pages, in the
    /// marketplace's currency. A marketplace's list holds payouts in other currencies too (Amazon lists
    /// every North America payout with the United States); they count at their converted amounts, and
    /// those still waiting for an exchange rate are counted apart instead of added in.
    /// </summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Search as for <see cref="GetSettlementsAsync"/>; paging is ignored.</param>
    /// <param name="filter">Period range and currency; the sort is ignored.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The totals, or <see langword="null"/> when nothing matches.</returns>
    Task<SettlementTotals?> GetSettlementTotalsAsync(string marketplaceId, PageRequest request, SettlementListFilter? filter, CancellationToken cancellationToken);

    /// <summary>The currencies the settlements a search and period range select were paid in (for the currency filter).</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Search as for <see cref="GetSettlementsAsync"/>; paging is ignored.</param>
    /// <param name="filter">Period range; its currency and sort are ignored.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The currencies, most settlements first.</returns>
    Task<IReadOnlyList<string>> GetSettlementCurrenciesAsync(string marketplaceId, PageRequest request, SettlementListFilter? filter, CancellationToken cancellationToken);

    /// <summary>Gets one settlement's totals.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The settlement, or <see langword="null"/> when it does not exist in the marketplace.</returns>
    Task<SettlementSummary?> GetSettlementAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken);

    /// <summary>Lists a settlement broken down by SKU, sale price, and currency.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="request">Paging; search matches SKU, ASIN, or title.</param>
    /// <param name="filter">Which rows and their order; <see langword="null"/> for <see cref="SettlementSkuFilter.Default"/> (largest sales first).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of summary rows.</returns>
    Task<PagedResult<SettlementSkuSummary>> GetSettlementSkuSummaryAsync(string marketplaceId, string settlementId, PageRequest request, SettlementSkuFilter? filter, CancellationToken cancellationToken);

    /// <summary>Gets one row of the per-SKU settlement summary.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="sku">The row's SKU; <see langword="null"/> for the row of amounts that belong to no SKU.</param>
    /// <param name="salePrice">The row's sale price; <see langword="null"/> when it has none.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, or <see langword="null"/> when no such group exists.</returns>
    Task<SettlementSkuSummary?> GetSettlementSkuGroupAsync(string marketplaceId, string settlementId, string? sku, decimal? salePrice, CancellationToken cancellationToken);

    /// <summary>
    /// Lists the settlement lines behind one per-SKU summary row, grouped by order (lines without an
    /// order are grouped by transaction type). Pages count groups, so an order's lines are never split.
    /// </summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="sku">The row's SKU; <see langword="null"/> for amounts that belong to no SKU.</param>
    /// <param name="salePrice">The row's sale price; <see langword="null"/> when it has none.</param>
    /// <param name="request">Paging; search matches order id, transaction type, amount type, or description. A group matches when any of its lines does, and is then listed whole.</param>
    /// <param name="filter">Bucket and order; <see langword="null"/> for <see cref="SettlementLineFilter.Default"/> (earliest posted first).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of groups, each with all its lines.</returns>
    Task<PagedResult<SettlementOrderGroup>> GetSettlementOrderGroupsAsync(string marketplaceId, string settlementId, string? sku, decimal? salePrice, PageRequest request, SettlementLineFilter? filter, CancellationToken cancellationToken);
}
