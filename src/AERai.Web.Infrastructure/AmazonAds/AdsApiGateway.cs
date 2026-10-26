using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>
/// Live <see cref="IAmazonAdsGateway"/> over <see cref="AdsApiClient"/>. Finds the marketplace's advertising
/// profile from the profiles the authorization reaches, so no profile id has to be configured.
/// </summary>
/// <param name="client">Typed Ads API client.</param>
/// <param name="profiles">Profile cache.</param>
internal sealed class AdsApiGateway(AdsApiClient client, AdsProfileCache profiles) : IAmazonAdsGateway
{
    /// <inheritdoc/>
    public async Task<string> RequestSpendReportAsync(Marketplace marketplace, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var profileId = await ProfileIdAsync(marketplace, cancellationToken).ConfigureAwait(false);
        return await client.CreateSpendReportAsync(marketplace.Region, profileId, start, end, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<AdsReportStatus> GetReportStatusAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        var report = await GetReportAsync(marketplace, reportId, cancellationToken).ConfigureAwait(false);
        return new AdsReportStatus(ParseState(report.Status), report.FailureReason);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReportAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        // The download URL expires within the hour, so it is read fresh rather than carried from the status check.
        var report = await GetReportAsync(marketplace, reportId, cancellationToken).ConfigureAwait(false);
        var url = report.Url ?? throw new InvalidOperationException($"Amazon Ads report {reportId} has no download link (status {report.Status}).");
        return await client.DownloadAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private async Task<ReportResponse> GetReportAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportId);

        var profileId = await ProfileIdAsync(marketplace, cancellationToken).ConfigureAwait(false);
        return await client.GetReportAsync(marketplace.Region, profileId, reportId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// The seller profile whose account is in the marketplace. An authorization can reach several accounts
    /// (seller, vendor, agency); the seller one is preferred because the app's SKUs are seller SKUs.
    /// </summary>
    private async Task<string> ProfileIdAsync(Marketplace marketplace, CancellationToken cancellationToken)
    {
        var found = await FindAsync(marketplace, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            // A newly authorized account would not be in a cached list yet.
            profiles.Invalidate(marketplace.Region);
            found = await FindAsync(marketplace, cancellationToken).ConfigureAwait(false);
        }

        return found?.ProfileId.ToString(CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException(
                $"No Amazon Ads profile for {marketplace.Name} ({marketplace.MarketplaceId}). Authorize the advertising account for this marketplace with the AmazonAds refresh token's Amazon login.");
    }

    private async Task<AdsProfile?> FindAsync(Marketplace marketplace, CancellationToken cancellationToken)
    {
        var all = await profiles.GetAsync(marketplace.Region, ct => client.ListProfilesAsync(marketplace.Region, ct), cancellationToken).ConfigureAwait(false);
        return all
            .Where(p => string.Equals(p.AccountInfo?.MarketplaceStringId, marketplace.MarketplaceId, StringComparison.Ordinal))
            .OrderBy(p => string.Equals(p.AccountInfo?.Type, "seller", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault();
    }

    private static AdsReportState ParseState(string? status) => status switch
    {
        "PENDING" or "PROCESSING" => AdsReportState.Pending,
        "COMPLETED" => AdsReportState.Completed,
        "FAILED" => AdsReportState.Failed,
        _ => throw new InvalidOperationException($"Unknown Amazon Ads report status '{status}'."),
    };
}
