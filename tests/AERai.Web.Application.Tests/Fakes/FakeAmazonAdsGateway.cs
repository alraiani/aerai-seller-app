using System.IO.Compression;
using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IAmazonAdsGateway"/>: records each request, reports the scripted states in order
/// (then <see cref="AdsReportState.Completed"/>), and serves <see cref="ReportJson"/> gzip-compressed.
/// </summary>
internal sealed class FakeAmazonAdsGateway : IAmazonAdsGateway
{
    public List<(string MarketplaceId, DateOnly Start, DateOnly End)> Requests { get; } = [];

    public Queue<AdsReportStatus> Statuses { get; } = new();

    public string ReportJson { get; set; } = "[]";

    public Exception? Failure { get; set; }

    public Task<string> RequestSpendReportAsync(Marketplace marketplace, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        if (Failure is not null)
        {
            throw Failure;
        }

        Requests.Add((marketplace.MarketplaceId, start, end));
        return Task.FromResult($"ads-report-{Requests.Count}");
    }

    public Task<AdsReportStatus> GetReportStatusAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken) =>
        Task.FromResult(Statuses.Count > 0 ? Statuses.Dequeue() : new AdsReportStatus(AdsReportState.Completed, null));

    public Task<Stream> OpenReportAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(Encoding.UTF8.GetBytes(ReportJson));
        }

        buffer.Position = 0;
        return Task.FromResult<Stream>(buffer);
    }
}
