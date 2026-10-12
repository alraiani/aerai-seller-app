using AERai.Web.Application.Common;
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

    /// <summary>Lists settlements, newest period first.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Paging; search matches settlement id.</param>
    /// <returns>One page of settlements.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<SettlementSummary>> GetSettlementsAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken);

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
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of summary rows, largest sales first.</returns>
    Task<PagedResult<SettlementSkuSummary>> GetSettlementSkuSummaryAsync(string marketplaceId, string settlementId, PageRequest request, CancellationToken cancellationToken);

    /// <summary>Gets one row of the per-SKU settlement summary.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="sku">The row's SKU; <see langword="null"/> for the row of amounts that belong to no SKU.</param>
    /// <param name="salePrice">The row's sale price; <see langword="null"/> when it has none.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, or <see langword="null"/> when no such group exists.</returns>
    Task<SettlementSkuSummary?> GetSettlementSkuGroupAsync(string marketplaceId, string settlementId, string? sku, decimal? salePrice, CancellationToken cancellationToken);

    /// <summary>Lists the individual settlement lines behind one per-SKU summary row.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="sku">The row's SKU; <see langword="null"/> for amounts that belong to no SKU.</param>
    /// <param name="salePrice">The row's sale price; <see langword="null"/> when it has none.</param>
    /// <param name="request">Paging; search matches order id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One page of lines, oldest posted first.</returns>
    Task<PagedResult<SettlementLineDetail>> GetSettlementLinesAsync(string marketplaceId, string settlementId, string? sku, decimal? salePrice, PageRequest request, CancellationToken cancellationToken);
}
