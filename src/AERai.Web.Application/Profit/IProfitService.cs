using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Profit;

/// <summary>
/// Profit &amp; loss by the day Amazon posted each amount (accrual): sales, refunds, Amazon's fees, advertising, and
/// cost of goods per SKU, and how it lines up with the settlements Amazon paid.
/// </summary>
public interface IProfitService
{
    /// <summary>Gets the profit page's report.</summary>
    /// <param name="marketplace">Marketplace (its currency, time zone, and costs).</param>
    /// <param name="filter">Range, filters, and sort.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report.</returns>
    Task<ProfitReport> GetReportAsync(Marketplace marketplace, ProfitFilter filter, CancellationToken cancellationToken);

    /// <summary>Gets one SKU's profit by period.</summary>
    /// <param name="marketplace">Marketplace.</param>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="from">First local day.</param>
    /// <param name="to">Last local day (inclusive).</param>
    /// <param name="fullyLoaded">Whether amounts not tied to a SKU are spread across SKUs by sales share.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report, or <see langword="null"/> when the SKU has nothing in the range.</returns>
    Task<ProfitSkuReport?> GetSkuAsync(Marketplace marketplace, string sku, DateOnly from, DateOnly to, bool fullyLoaded, CancellationToken cancellationToken);

    /// <summary>Gets how a range's profit lines up with the settlements whose period ends in it.</summary>
    /// <param name="marketplace">Marketplace.</param>
    /// <param name="from">First local day.</param>
    /// <param name="to">Last local day (inclusive).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The reconciliation.</returns>
    Task<ProfitReconciliation> GetReconciliationAsync(Marketplace marketplace, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}
