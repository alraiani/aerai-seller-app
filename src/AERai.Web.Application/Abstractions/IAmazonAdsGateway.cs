using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// The app's view of the Amazon Ads API reporting (v3): request a Sponsored Products advertised product
/// report, check on it, and download it. The live implementation finds the marketplace's advertising
/// profile and calls the Ads API through its own pipeline (it is not SP-API); the simulated one makes up
/// spend for local development.
/// </summary>
public interface IAmazonAdsGateway
{
    /// <summary>Asks Amazon Ads for daily spend per advertised SKU and campaign over a range of days.</summary>
    /// <param name="marketplace">Marketplace whose advertising profile is reported.</param>
    /// <param name="start">First day, in the account's time zone.</param>
    /// <param name="end">Last day (inclusive); at most 31 days after <paramref name="start"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Amazon's report id.</returns>
    Task<string> RequestSpendReportAsync(Marketplace marketplace, DateOnly start, DateOnly end, CancellationToken cancellationToken);

    /// <summary>Gets a report's processing status.</summary>
    /// <param name="marketplace">Marketplace the report was requested for.</param>
    /// <param name="reportId">Amazon report id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The status.</returns>
    Task<AdsReportStatus> GetReportStatusAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken);

    /// <summary>Downloads a completed report exactly as Amazon serves it (gzip-compressed JSON).</summary>
    /// <param name="marketplace">Marketplace the report was requested for.</param>
    /// <param name="reportId">Amazon report id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report's bytes; the caller disposes the stream. Read it with <see cref="AdSpendReportParser"/>.</returns>
    Task<Stream> OpenReportAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken);
}
