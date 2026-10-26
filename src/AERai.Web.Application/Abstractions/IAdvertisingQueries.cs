using AERai.Web.Application.Advertising;

namespace AERai.Web.Application.Abstractions;

/// <summary>Read-only rows for the Advertising page. Every query is scoped to one marketplace; rules live in <see cref="AdvertisingService"/>.</summary>
public interface IAdvertisingQueries
{
    /// <summary>Each advertised SKU's figures over a range of days, with its catalog details. <see cref="AdMetrics.TotalSales"/> is not filled.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="from">First day.</param>
    /// <param name="to">Last day (inclusive).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One row per SKU with spend, clicks, or impressions in the range.</returns>
    Task<IReadOnlyList<AdSkuRow>> GetSkuSpendAsync(string marketplaceId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    /// <summary>Units and sales per SKU over a period (cancelled orders left out), for the given SKUs.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="from">Start of the period.</param>
    /// <param name="before">End of the period (exclusive).</param>
    /// <param name="skus">SKUs to add up.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Units and sales keyed by SKU (case-insensitive); SKUs without sales are absent.</returns>
    Task<IReadOnlyDictionary<string, (int Units, decimal Sales)>> GetSalesBySkuAsync(
        string marketplaceId, DateTimeOffset from, DateTimeOffset before, IReadOnlyCollection<string> skus, CancellationToken cancellationToken);

    /// <summary>The latest day with ad spend stored for the marketplace.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The day, or <see langword="null"/> when none is stored.</returns>
    Task<DateOnly?> GetLatestSpendDateAsync(string marketplaceId, CancellationToken cancellationToken);
}
