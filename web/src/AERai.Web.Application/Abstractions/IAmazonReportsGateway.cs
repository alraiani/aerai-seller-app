using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// The app's view of Amazon's SP-API Reports API: request a report, check it, list available ones,
/// and download its document. Implemented in Infrastructure over the SP-API (or a simulator for local use).
/// </summary>
/// <remarks>
/// Every call goes through the Infrastructure SP-API pipeline (LWA token, per-operation rate limits,
/// retry with backoff). Application code never talks to Amazon directly.
/// </remarks>
public interface IAmazonReportsGateway
{
    /// <summary>Asks Amazon to generate a report.</summary>
    /// <param name="reportType">Report to generate (must be an on-demand type).</param>
    /// <param name="dataStart">Start of the data window, or <see langword="null"/> for snapshot reports.</param>
    /// <param name="dataEnd">End of the data window, or <see langword="null"/> for snapshot reports.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Amazon's report id.</returns>
    Task<string> RequestReportAsync(AmazonReportType reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken);

    /// <summary>Gets a report's processing status.</summary>
    /// <param name="reportId">Amazon report id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The status and, when done, the document id.</returns>
    Task<AmazonReportStatus> GetReportStatusAsync(string reportId, CancellationToken cancellationToken);

    /// <summary>Lists completed reports of a type created since a point in time, oldest first.</summary>
    /// <param name="reportType">Report type.</param>
    /// <param name="createdSince">Lower bound on report creation time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Completed reports with their document ids.</returns>
    Task<IReadOnlyList<AvailableAmazonReport>> ListCompletedReportsAsync(AmazonReportType reportType, DateTimeOffset createdSince, CancellationToken cancellationToken);

    /// <summary>Downloads a report document, decompressed, as a readable stream.</summary>
    /// <param name="reportDocumentId">Document id from <see cref="GetReportStatusAsync"/> or <see cref="ListCompletedReportsAsync"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The document content; the caller disposes it.</returns>
    Task<Stream> OpenReportDocumentAsync(string reportDocumentId, CancellationToken cancellationToken);
}
