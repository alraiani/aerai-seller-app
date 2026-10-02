using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Gateway registered when <see cref="SpApiMode.Disabled"/>: every call fails with a clear message.
/// The scheduler and "Run now" check <see cref="IAmazonConnectionInfo.CanRun"/> first, so this is a backstop.
/// </summary>
internal sealed class UnavailableReportsGateway : IAmazonReportsGateway
{
    private const string Message = "Amazon SP-API is disabled. Set SpApi:Mode to Live (with credentials) or Simulated.";

    /// <inheritdoc/>
    public Task<string> RequestReportAsync(AmazonReportType reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<AmazonReportStatus> GetReportStatusAsync(string reportId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<IReadOnlyList<AvailableAmazonReport>> ListCompletedReportsAsync(AmazonReportType reportType, DateTimeOffset createdSince, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<Stream> OpenReportDocumentAsync(string reportDocumentId, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);
}
