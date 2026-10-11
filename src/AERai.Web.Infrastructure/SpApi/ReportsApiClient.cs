using System.Globalization;
using System.IO.Compression;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Typed client for the SP-API Reports API (version 2021-06-30). Every call is tagged with its
/// operation name and sent through <see cref="SpApiPipelineHandler"/>.
/// </summary>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="httpClientFactory">Factory for the separate document-download client.</param>
/// <param name="options">SP-API settings (regional endpoints).</param>
internal sealed class ReportsApiClient(HttpClient http, IHttpClientFactory httpClientFactory, IOptions<SpApiOptions> options)
{
    /// <summary>
    /// Named client for report documents. Documents are served from pre-signed S3 URLs, so this
    /// client deliberately bypasses the pipeline: the SP-API access token must never be sent to S3.
    /// </summary>
    public const string DocumentHttpClientName = "SpApiDocuments";

    private const string BasePath = "reports/2021-06-30";

    /// <summary>Calls <c>createReport</c> for one marketplace.</summary>
    /// <param name="marketplace">Marketplace to report on; its region selects endpoint and credentials.</param>
    /// <param name="reportType">SP-API report type code.</param>
    /// <param name="dataStart">Optional data window start.</param>
    /// <param name="dataEnd">Optional data window end.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new report id.</returns>
    public async Task<string> CreateReportAsync(Marketplace marketplace, string reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var body = new CreateReportRequest(reportType, [marketplace.MarketplaceId], dataStart?.ToUniversalTime(), dataEnd?.ToUniversalTime());
        using var request = Request(HttpMethod.Post, marketplace.Region, $"{BasePath}/reports");
        request.Content = JsonContent.Create(body);
        var result = await SendAsync<CreateReportResponse>(request, SpApiOperation.CreateReport, cancellationToken).ConfigureAwait(false);
        return result.ReportId;
    }

    /// <summary>Calls <c>getReport</c>.</summary>
    /// <param name="region">Region the report was requested in.</param>
    /// <param name="reportId">Report id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report.</returns>
    public Task<ReportResponse> GetReportAsync(AmazonRegion region, string reportId, CancellationToken cancellationToken)
    {
        var request = Request(HttpMethod.Get, region, $"{BasePath}/reports/{Uri.EscapeDataString(reportId)}");
        return SendAndDisposeAsync<ReportResponse>(request, SpApiOperation.GetReport, cancellationToken);
    }

    /// <summary>Calls <c>getReports</c> for one marketplace's DONE reports of one type, following <c>nextToken</c> pages.</summary>
    /// <param name="marketplace">Marketplace whose reports to list.</param>
    /// <param name="reportType">SP-API report type code.</param>
    /// <param name="createdSince">Lower bound on creation time.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>All matching reports.</returns>
    public async Task<IReadOnlyList<ReportResponse>> GetDoneReportsAsync(Marketplace marketplace, string reportType, DateTimeOffset createdSince, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var results = new List<ReportResponse>();
        string? nextToken = null;
        do
        {
            // When nextToken is present SP-API requires it to be the only query parameter.
            var query = nextToken is null
                ? $"reportTypes={Uri.EscapeDataString(reportType)}&processingStatuses=DONE&pageSize=100" +
                  $"&marketplaceIds={Uri.EscapeDataString(marketplace.MarketplaceId)}" +
                  $"&createdSince={Uri.EscapeDataString(createdSince.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture))}"
                : $"nextToken={Uri.EscapeDataString(nextToken)}";

            var page = await SendAndDisposeAsync<GetReportsResponse>(
                Request(HttpMethod.Get, marketplace.Region, $"{BasePath}/reports?{query}"), SpApiOperation.GetReports, cancellationToken).ConfigureAwait(false);

            results.AddRange(page.Reports ?? []);
            nextToken = page.NextToken;
        }
        while (nextToken is not null);

        return results;
    }

    /// <summary>
    /// Calls <c>getReports</c> for a single page of one report, purely to prove that the token
    /// exchange and authorization work. The response content is ignored.
    /// </summary>
    /// <param name="region">Region whose credentials to test.</param>
    /// <param name="reportType">SP-API report type code (any type the app is authorized for).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when Amazon accepted the call; throws on failure.</returns>
    public async Task PingAsync(AmazonRegion region, string reportType, CancellationToken cancellationToken)
    {
        var request = Request(HttpMethod.Get, region, $"{BasePath}/reports?reportTypes={Uri.EscapeDataString(reportType)}&pageSize=1");
        await SendAndDisposeAsync<GetReportsResponse>(request, SpApiOperation.GetReports, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Calls <c>getReportDocument</c>, then downloads and (if needed) decompresses the document.</summary>
    /// <param name="region">Region the report belongs to.</param>
    /// <param name="reportDocumentId">Document id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The decompressed document stream; the caller disposes it.</returns>
    public async Task<Stream> OpenDocumentAsync(AmazonRegion region, string reportDocumentId, CancellationToken cancellationToken)
    {
        var document = await SendAndDisposeAsync<ReportDocumentResponse>(
            Request(HttpMethod.Get, region, $"{BasePath}/documents/{Uri.EscapeDataString(reportDocumentId)}"),
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

    /// <summary>
    /// Builds a request against the region's endpoint and tags it with the region, so the pipeline
    /// uses that region's access token and rate-limit buckets.
    /// </summary>
    private HttpRequestMessage Request(HttpMethod method, AmazonRegion region, string relativePath)
    {
        var request = new HttpRequestMessage(method, new Uri(options.Value.EndpointFor(region), relativePath));
        request.Options.Set(SpApiOperation.RegionKey, region);
        return request;
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
