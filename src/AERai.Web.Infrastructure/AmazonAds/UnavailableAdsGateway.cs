using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>
/// Ads gateway registered when <see cref="AmazonAdsMode.Disabled"/>: every call fails with a clear message,
/// which the run records in its history.
/// </summary>
internal sealed class UnavailableAdsGateway : IAmazonAdsGateway
{
    private const string Message = "The Amazon Ads API is disabled. Set AmazonAds:Mode to Live (with AmazonAds:ClientId, ClientSecret, and RefreshToken) or Simulated.";

    /// <inheritdoc/>
    public Task<string> RequestSpendReportAsync(Marketplace marketplace, DateOnly start, DateOnly end, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<AdsReportStatus> GetReportStatusAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task CancelSpendReportAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<Stream> OpenReportAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);
}
