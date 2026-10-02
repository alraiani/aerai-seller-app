using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>Scriptable <see cref="IAmazonReportsGateway"/>: tests set statuses, documents, and listings.</summary>
internal sealed class FakeAmazonReportsGateway : IAmazonReportsGateway
{
    public List<(AmazonReportType Type, DateTimeOffset? Start, DateTimeOffset? End)> Requests { get; } = [];

    /// <summary>Statuses returned by successive GetReportStatusAsync calls (last one repeats).</summary>
    public Queue<AmazonReportStatus> Statuses { get; } = new();

    public Dictionary<string, string> Documents { get; } = new(StringComparer.Ordinal);

    public List<AvailableAmazonReport> Available { get; } = [];

    public Exception? ThrowOnRequest { get; set; }

    public Task<string> RequestReportAsync(AmazonReportType reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken)
    {
        if (ThrowOnRequest is not null)
        {
            throw ThrowOnRequest;
        }

        Requests.Add((reportType, dataStart, dataEnd));
        return Task.FromResult($"R{Requests.Count}");
    }

    public Task<AmazonReportStatus> GetReportStatusAsync(string reportId, CancellationToken cancellationToken) =>
        Task.FromResult(Statuses.Count > 1 ? Statuses.Dequeue() : Statuses.Peek());

    public Task<IReadOnlyList<AvailableAmazonReport>> ListCompletedReportsAsync(AmazonReportType reportType, DateTimeOffset createdSince, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<AvailableAmazonReport>>(Available.Where(r => r.CreatedAt >= createdSince).ToList());

    public Task<Stream> OpenReportDocumentAsync(string reportDocumentId, CancellationToken cancellationToken) =>
        Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(Documents[reportDocumentId])));
}
