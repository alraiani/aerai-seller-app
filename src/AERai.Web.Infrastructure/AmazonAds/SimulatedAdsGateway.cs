using System.Collections.Concurrent;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.SpApi;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>
/// Ads gateway for <see cref="AmazonAdsMode.Simulated"/>: four of the simulated catalog's SKUs advertised in
/// two campaigns, with spend derived from the SKU and day so repeated runs give the same numbers. A report is
/// still generating at its first check and ready at the next, so the waiting path is exercised locally. The
/// download is gzip JSON in Amazon's row shape. Nothing leaves the machine.
/// </summary>
internal sealed class SimulatedAdsGateway : IAmazonAdsGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly (string Id, string Name)[] Campaigns =
    [
        ("310000000001", "SP - Auto"),
        ("310000000002", "SP - Exact match"),
    ];

    private readonly ConcurrentDictionary<string, Requested> _reports = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<string> RequestSpendReportAsync(Marketplace marketplace, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var reportId = Guid.NewGuid().ToString("D");
        _reports[reportId] = new Requested(marketplace.Currency, start, end);
        return Task.FromResult(reportId);
    }

    /// <inheritdoc/>
    public Task<AdsReportStatus> GetReportStatusAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        // An unknown id is a report requested before a restart; the simulator has lost it, as S3 would after expiry.
        if (!_reports.TryGetValue(reportId, out var report))
        {
            return Task.FromResult(new AdsReportStatus(AdsReportState.Failed, "The simulator no longer has this report (the app restarted)."));
        }

        var checks = Interlocked.Increment(ref report.Checks);
        return Task.FromResult(new AdsReportStatus(checks > 1 ? AdsReportState.Completed : AdsReportState.Pending, null));
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReportAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        if (!_reports.TryRemove(reportId, out var report))
        {
            throw new InvalidOperationException($"Simulated Amazon Ads report {reportId} does not exist.");
        }

        var rows = new List<object>();
        for (var day = report.Start; day <= report.End; day = day.AddDays(1))
        {
            foreach (var (sku, asin, _, price) in SimulatedReportsGateway.Catalog.Take(4))
            {
                foreach (var (campaignId, campaignName) in Campaigns)
                {
                    var h = Hash($"{sku}|{day:yyyy-MM-dd}|{campaignId}");

                    // Most days some ads get no clicks at all.
                    var impressions = 200 + (int)(h % 1800);
                    var clicks = h % 5 == 0 ? 0 : 1 + (int)(h % 14);
                    var cost = Math.Round(clicks * (0.45m + (h % 90) / 100m), 2);
                    var orders = clicks == 0 ? 0 : (int)(h % 4) == 0 ? 0 : 1 + (int)(h % 2);
                    rows.Add(new Dictionary<string, object?>
                    {
                        ["date"] = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["advertisedSku"] = sku,
                        ["advertisedAsin"] = asin,
                        ["campaignId"] = long.Parse(campaignId, CultureInfo.InvariantCulture),
                        ["campaignName"] = campaignName,
                        ["impressions"] = impressions,
                        ["clicks"] = clicks,
                        ["cost"] = cost,
                        ["sales7d"] = orders * price,
                        ["purchases7d"] = orders,
                        ["unitsSoldClicks7d"] = orders,
                        ["campaignBudgetCurrencyCode"] = report.Currency,
                    });
                }
            }
        }

        var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            JsonSerializer.Serialize(gzip, rows, JsonOptions);
        }

        buffer.Position = 0;
        return Task.FromResult<Stream>(buffer);
    }

    // Stable across processes, unlike string.GetHashCode.
    private static uint Hash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }

    /// <summary>A requested report and how often it has been checked.</summary>
    private sealed class Requested(string currency, DateOnly start, DateOnly end)
    {
        public string Currency { get; } = currency;

        public DateOnly Start { get; } = start;

        public DateOnly End { get; } = end;

#pragma warning disable SA1401, CA1051 // A field so Interlocked can count checks from concurrent callers.
        public int Checks;
#pragma warning restore SA1401, CA1051
    }
}
