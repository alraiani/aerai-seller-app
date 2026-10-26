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
}
