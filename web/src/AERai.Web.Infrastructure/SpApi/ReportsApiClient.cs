using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Typed client for the SP-API Reports API (version 2021-06-30). Every call is tagged with its
/// operation name and sent through <see cref="SpApiPipelineHandler"/>.
/// </summary>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="httpClientFactory">Factory for the separate document-download client.</param>
/// <param name="options">SP-API settings (marketplace).</param>
internal sealed class ReportsApiClient(HttpClient http, IHttpClientFactory httpClientFactory, IOptions<SpApiOptions> options)
{
    /// <summary>
    /// Named client for report documents. Documents are served from pre-signed S3 URLs, so this
    /// client deliberately bypasses the pipeline: the SP-API access token must never be sent to S3.
    /// </summary>
    public const string DocumentHttpClientName = "SpApiDocuments";

    private const string BasePath = "reports/2021-06-30";

    /// <summary>Calls <c>createReport</c>.</summary>
    /// <param name="reportType">SP-API report type code.</param>
    /// <param name="dataStart">Optional data window start.</param>
    /// <param name="dataEnd">Optional data window end.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new report id.</returns>
    public async Task<string> CreateReportAsync(string reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken)
    {
        var body = new CreateReportRequest(reportType, [options.Value.MarketplaceId], dataStart?.ToUniversalTime(), dataEnd?.ToUniversalTime());
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BasePath}/reports") { Content = JsonContent.Create(body) };
        var result = await SendAsync<CreateReportResponse>(request, SpApiOperation.CreateReport, cancellationToken).ConfigureAwait(false);
        return result.ReportId;
    }

    /// <summary>Calls <c>getReport</c>.</summary>
    /// <param name="reportId">Report id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report.</returns>
    public Task<ReportResponse> GetReportAsync(string reportId, CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/reports/{Uri.EscapeDataString(reportId)}");
        return SendAndDisposeAsync<ReportResponse>(request, SpApiOperation.GetReport, cancellationToken);
    }

    /// <summary>Calls <c>getReports</c> for DONE reports of one type, following <c>nextToken</c> pages.</summary>
    /// <param name="reportType">SP-API report type code.</param>
    /// <param name="createdSince">Lower bound on creation time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>All matching reports.</returns>
    public async Task<IReadOnlyList<ReportResponse>> GetDoneReportsAsync(string reportType, DateTimeOffset createdSince, CancellationToken cancellationToken)
    {
        var results = new List<ReportResponse>();
        string? nextToken = null;
        do
        {
            // When nextToken is present SP-API requires it to be the only query parameter.
            var query = nextToken is null
                ? $"reportTypes={Uri.EscapeDataString(reportType)}&processingStatuses=DONE&pageSize=100" +
                  $"&createdSince={Uri.EscapeDataString(createdSince.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}"
                : $"nextToken={Uri.EscapeDataString(nextToken)}";

            var page = await SendAndDisposeAsync<GetReportsResponse>(
                new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/reports?{query}"), SpApiOperation.GetReports, cancellationToken).ConfigureAwait(false);

            results.AddRange(page.Reports ?? []);
            nextToken = page.NextToken;
        }
        while (nextToken is not null);

        return results;
    }

    /// <summary>Calls <c>getReportDocument</c>, then downloads and (if needed) decompresses the document.</summary>
    /// <param name="reportDocumentId">Document id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The decompressed document stream; the caller disposes it.</returns>
    public async Task<Stream> OpenDocumentAsync(string reportDocumentId, CancellationToken cancellationToken)
    {
        var document = await SendAndDisposeAsync<ReportDocumentResponse>(
            new HttpRequestMessage(HttpMethod.Get, $"{BasePath}/documents/{Uri.EscapeDataString(reportDocumentId)}"),
            SpApiOperation.GetReportDocument,
            cancellationToken).ConfigureAwait(false);

        var response = await httpClientFactory.CreateClient(DocumentHttpClientName)
            .GetAsync(document.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        return string.Equals(document.CompressionAlgorithm, "GZIP", StringComparison.OrdinalIgnoreCase)
            ? new GZipStream(stream, CompressionMode.Decompress)
            : stream;
    }

    private async Task<T> SendAndDisposeAsync<T>(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        using (request)
        {
            return await SendAsync<T>(request, operation, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        request.Options.Set(SpApiOperation.OptionKey, operation);
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // SP-API error bodies ({"errors":[{code,message}]}) contain no secrets and help diagnose the call.
            var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException($"SP-API {operation} failed with HTTP {(int)response.StatusCode}: {Truncate(detail, 500)}", null, response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"SP-API {operation} returned an empty body.");
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    private sealed record CreateReportRequest(
        string ReportType,
        IReadOnlyList<string> MarketplaceIds,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? DataStartTime,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] DateTimeOffset? DataEndTime);

    private sealed record CreateReportResponse(string ReportId);

    private sealed record GetReportsResponse(IReadOnlyList<ReportResponse>? Reports, string? NextToken);

    private sealed record ReportDocumentResponse(string ReportDocumentId, Uri Url, string? CompressionAlgorithm);
}

/// <summary>A report as returned by <c>getReport</c> / <c>getReports</c>.</summary>
/// <param name="ReportId">Report id.</param>
/// <param name="ReportType">Report type code.</param>
/// <param name="ProcessingStatus">IN_QUEUE, IN_PROGRESS, DONE, CANCELLED, or FATAL.</param>
/// <param name="ReportDocumentId">Document id when DONE.</param>
/// <param name="CreatedTime">Creation time.</param>
internal sealed record ReportResponse(string ReportId, string ReportType, string ProcessingStatus, string? ReportDocumentId, DateTimeOffset CreatedTime);
