using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>Scriptable <see cref="IAmazonReportsGateway"/>: tests set statuses, documents, and listings.</summary>
internal sealed class FakeAmazonReportsGateway : IAmazonReportsGateway
{
    public List<(AmazonReportType Type, DateTimeOffset? Start, DateTimeOffset? End)> Requests { get; } = [];

    /// <summary>Marketplace ids passed to every gateway call, in order.</summary>
    public List<string> MarketplacesSeen { get; } = [];

    /// <summary>Statuses returned by successive GetReportStatusAsync calls (last one repeats).</summary>
    public Queue<AmazonReportStatus> Statuses { get; } = new();

    public Dictionary<string, string> Documents { get; } = new(StringComparer.Ordinal);

    public List<AvailableAmazonReport> Available { get; } = [];

    public Exception? ThrowOnRequest { get; set; }

    public Task<string> RequestReportAsync(Marketplace marketplace, AmazonReportType reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken)
    {
        MarketplacesSeen.Add(marketplace.MarketplaceId);
        if (ThrowOnRequest is not null)
        {
            throw ThrowOnRequest;
        }

        Requests.Add((reportType, dataStart, dataEnd));
        return Task.FromResult($"R{Requests.Count}");
    }

    public Task<AmazonReportStatus> GetReportStatusAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Statuses.Count > 1 ? Statuses.Dequeue() : Statuses.Peek());
    }

    public Task<IReadOnlyList<AvailableAmazonReport>> ListCompletedReportsAsync(Marketplace marketplace, AmazonReportType reportType, DateTimeOffset createdSince, CancellationToken cancellationToken)
    {
        MarketplacesSeen.Add(marketplace.MarketplaceId);
        return Task.FromResult<IReadOnlyList<AvailableAmazonReport>>(Available.Where(r => r.CreatedAt >= createdSince).ToList());
    }

    public Task<Stream> OpenReportDocumentAsync(Marketplace marketplace, string reportDocumentId, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Documents[reportDocumentId])));
}
