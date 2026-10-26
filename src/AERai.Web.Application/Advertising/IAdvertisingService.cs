using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Advertising;

/// <summary>The Advertising page: Sponsored Products spend by family and SKU, with ACoS and TACoS.</summary>
public interface IAdvertisingService
{
    /// <summary>Builds the page for a marketplace and range of days.</summary>
    /// <param name="marketplace">Marketplace shown.</param>
    /// <param name="filter">Days, family, search, and sort.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report.</returns>
    Task<AdvertisingReport> GetReportAsync(Marketplace marketplace, AdvertisingFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// Ad spend by family over the local days a list of settlements covers: from the day the earliest
    /// period starts through the day the latest ends, in the marketplace's time zone, at most
    /// <see cref="AdvertisingService.MaxDays"/> (the latest days are kept).
    /// </summary>
    /// <param name="marketplace">Marketplace shown.</param>
    /// <param name="periodStart">Start of the earliest listed settlement period, or <see langword="null"/> when none is listed.</param>
    /// <param name="periodEnd">End of the latest listed settlement period, or <see langword="null"/> when none is listed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Spend per family, with how far spend is synced.</returns>
    Task<SettlementAdSpend> GetSettlementSpendAsync(Marketplace marketplace, DateTimeOffset? periodStart, DateTimeOffset? periodEnd, CancellationToken cancellationToken);
}
