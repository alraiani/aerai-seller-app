using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Live <see cref="IAmazonReportsGateway"/> over <see cref="ReportsApiClient"/>.
/// </summary>
/// <param name="client">Typed Reports API client.</param>
internal sealed class SpApiReportsGateway(ReportsApiClient client) : IAmazonReportsGateway
{
    /// <inheritdoc/>
    public Task<string> RequestReportAsync(AmazonReportType reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken) =>
        client.CreateReportAsync(SpApiReportTypes.ToCode(reportType), dataStart, dataEnd, cancellationToken);

    /// <inheritdoc/>
    public async Task<AmazonReportStatus> GetReportStatusAsync(string reportId, CancellationToken cancellationToken)
    {
        var report = await client.GetReportAsync(reportId, cancellationToken).ConfigureAwait(false);
        return new AmazonReportStatus(ParseStatus(report.ProcessingStatus), report.ReportDocumentId);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<AvailableAmazonReport>> ListCompletedReportsAsync(AmazonReportType reportType, DateTimeOffset createdSince, CancellationToken cancellationToken)
    {
        var reports = await client.GetDoneReportsAsync(SpApiReportTypes.ToCode(reportType), createdSince, cancellationToken).ConfigureAwait(false);
        return reports
            .Where(r => r.ReportDocumentId is not null)
            .OrderBy(r => r.CreatedTime)
            .Select(r => new AvailableAmazonReport(r.ReportId, r.ReportDocumentId!, r.CreatedTime))
            .ToList();
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReportDocumentAsync(string reportDocumentId, CancellationToken cancellationToken) =>
        client.OpenDocumentAsync(reportDocumentId, cancellationToken);

    private static AmazonProcessingStatus ParseStatus(string status) => status switch
    {
        "IN_QUEUE" => AmazonProcessingStatus.InQueue,
        "IN_PROGRESS" => AmazonProcessingStatus.InProgress,
        "DONE" => AmazonProcessingStatus.Done,
        "CANCELLED" => AmazonProcessingStatus.Cancelled,
        "FATAL" => AmazonProcessingStatus.Fatal,
        _ => throw new InvalidOperationException($"Unknown SP-API processing status '{status}'."),
    };
}
