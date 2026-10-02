using AERai.Web.Application.Common;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Read-only queries over the <c>rpt</c> reporting views.
/// </summary>
public interface IReportingQueries
{
    /// <summary>Gets per-SKU daily sales for an inclusive UTC date range.</summary>
    /// <param name="from">First day (inclusive).</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <returns>Rows ordered by date, then SKU.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<IReadOnlyList<DailySalesBySku>> GetDailySalesAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Lists orders, newest first.</summary>
    /// <param name="request">Paging; search matches order id or status.</param>
    /// <returns>One page of orders.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<OrderSummary>> GetOrdersAsync(PageRequest request, CancellationToken cancellationToken);

    /// <summary>Lists each SKU's current inventory position, lowest days-of-supply first.</summary>
    /// <param name="request">Paging; search matches SKU or title.</param>
    /// <returns>One page of positions.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<InventoryPosition>> GetInventoryPositionsAsync(PageRequest request, CancellationToken cancellationToken);

    /// <summary>Gets SKUs whose days of supply is at or below a threshold.</summary>
    /// <param name="maxDaysOfSupply">Threshold in days.</param>
    /// <param name="take">Maximum rows to return.</param>
    /// <returns>At-risk positions, most urgent first.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<IReadOnlyList<InventoryPosition>> GetAtRiskInventoryAsync(decimal maxDaysOfSupply, int take, CancellationToken cancellationToken);

    /// <summary>Lists settlements, newest period first.</summary>
    /// <param name="request">Paging; search matches settlement id.</param>
    /// <returns>One page of settlements.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<PagedResult<SettlementSummary>> GetSettlementsAsync(PageRequest request, CancellationToken cancellationToken);
}
