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

    /// <summary>Lists each SKU's current inventory position, lowest days-of-supply first.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Paging; search matches SKU or title.</param>
    /// <returns>One page of positions.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<InventoryPosition>> GetInventoryPositionsAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken);

    /// <summary>Lists settlements, newest period first.</summary>
    /// <param name="marketplaceId">Marketplace to report on.</param>
    /// <param name="request">Paging; search matches settlement id.</param>
    /// <returns>One page of settlements.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<SettlementSummary>> GetSettlementsAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken);
}
