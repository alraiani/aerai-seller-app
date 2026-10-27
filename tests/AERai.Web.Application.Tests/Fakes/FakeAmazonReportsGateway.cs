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
    public List<DateTimeOffset> SinceSeen { get; } = [];

    public List<string> MarketplacesSeen { get; } = [];

    /// <summary>Statuses returned by successive GetReportStatusAsync calls (last one repeats).</summary>
    public Queue<AmazonReportStatus> Statuses { get; } = new();

    public Dictionary<string, string> Documents { get; } = new(StringComparer.Ordinal);

    public List<AvailableAmazonReport> Available { get; } = [];

    public Exception? ThrowOnRequest { get; set; }

    /// <summary>Report ids passed to CancelReportAsync.</summary>
    public List<string> CancelledReports { get; } = [];

    public Exception? ThrowOnCancel { get; set; }

    /// <summary>Runs inside RequestReportAsync before it answers (e.g. a user stopping the run mid-call).</summary>
    public Action? DuringRequest { get; set; }

    /// <summary>Runs inside GetReportStatusAsync before it answers (e.g. a user stopping the run mid-check).</summary>
    public Action? DuringStatus { get; set; }

    public Task<string> RequestReportAsync(Marketplace marketplace, AmazonReportType reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken)
    {
        MarketplacesSeen.Add(marketplace.MarketplaceId);
        DuringRequest?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        if (ThrowOnRequest is not null)
        {
            throw ThrowOnRequest;
        }

        Requests.Add((reportType, dataStart, dataEnd));
        return Task.FromResult($"R{Requests.Count}");
    }

    public Task<AmazonReportStatus> GetReportStatusAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        DuringStatus?.Invoke();
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Statuses.Count > 1 ? Statuses.Dequeue() : Statuses.Peek());
    }

    public Task CancelReportAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken)
    {
        CancelledReports.Add(reportId);
        return ThrowOnCancel is { } ex ? Task.FromException(ex) : Task.CompletedTask;
    }

    public Task<IReadOnlyList<AvailableAmazonReport>> ListCompletedReportsAsync(Marketplace marketplace, AmazonReportType reportType, DateTimeOffset createdSince, CancellationToken cancellationToken)
    {
        MarketplacesSeen.Add(marketplace.MarketplaceId);
        SinceSeen.Add(createdSince);
        return Task.FromResult<IReadOnlyList<AvailableAmazonReport>>(Available.Where(r => r.CreatedAt >= createdSince).ToList());
    }

    public Task<Stream> OpenReportDocumentAsync(Marketplace marketplace, string reportDocumentId, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Documents[reportDocumentId])));
}
