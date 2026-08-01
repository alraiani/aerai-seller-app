using System.IO.Compression;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AERai.Seller.SpApiClient.Reports;

/// <summary>Typed client for the SP-API Reports model (2021-06-30). All calls route through <see cref="SpApiRequestPipeline"/>.</summary>
public sealed class ReportsApiClient(SpApiRequestPipeline pipeline, ICredentialStore credentialStore, IHttpClientFactory httpClientFactory)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<string> CreateReportAsync(
        string reportType,
        DateTimeOffset? dataStartTime,
        DateTimeOffset? dataEndTime,
        CancellationToken cancellationToken)
    {
        var credentials = await credentialStore.GetCredentialsAsync(cancellationToken)
            ?? throw new InvalidOperationException("No SP-API credentials configured. Set them in Settings first.");

        var body = new Dictionary<string, object?>
        {
            ["reportType"] = reportType,
            ["marketplaceIds"] = new[] { credentials.MarketplaceId },
        };
        if (dataStartTime is not null) body["dataStartTime"] = dataStartTime.Value.ToString("O");
        if (dataEndTime is not null) body["dataEndTime"] = dataEndTime.Value.ToString("O");

        using var response = await pipeline.SendAsync(
            "Reports.CreateReport",
            host => new HttpRequestMessage(HttpMethod.Post, $"{host}/reports/2021-06-30/reports")
            {
                Content = new StringContent(JsonSerializer.Serialize(body, JsonOptions), Encoding.UTF8, "application/json"),
            },
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<CreateReportResult>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("CreateReport response was empty.");
        return result.ReportId;
    }

    public async Task<ReportStatus> GetReportAsync(string reportId, CancellationToken cancellationToken)
    {
        using var response = await pipeline.SendAsync(
            "Reports.GetReport",
            host => new HttpRequestMessage(HttpMethod.Get, $"{host}/reports/2021-06-30/reports/{reportId}"),
            cancellationToken);

        return await response.Content.ReadFromJsonAsync<ReportStatus>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("GetReport response was empty.");
    }

    public async Task<ReportDocument> GetReportDocumentAsync(string reportDocumentId, CancellationToken cancellationToken)
    {
        using var response = await pipeline.SendAsync(
            "Reports.GetReportDocument",
            host => new HttpRequestMessage(HttpMethod.Get, $"{host}/reports/2021-06-30/documents/{reportDocumentId}"),
            cancellationToken);

        return await response.Content.ReadFromJsonAsync<ReportDocument>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("GetReportDocument response was empty.");
    }

    /// <summary>
    /// Downloads the (possibly gzip-compressed) report document from its pre-signed URL and returns
    /// the decompressed text content. This is not an SP-API operation itself (no auth/rate limit needed)
    /// so it does not go through <see cref="SpApiRequestPipeline"/>, but reuses the same HttpClientFactory.
    /// </summary>
    public async Task<string> DownloadReportDocumentAsync(ReportDocument document, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(ReportsApiClient) + ".Download");
        await using var stream = await client.GetStreamAsync(document.Url, cancellationToken);

        if (string.Equals(document.CompressionAlgorithm, "GZIP", StringComparison.OrdinalIgnoreCase))
        {
            await using var gzipStream = new GZipStream(stream, CompressionMode.Decompress);
            using var decompressedReader = new StreamReader(gzipStream, Encoding.UTF8);
            return await decompressedReader.ReadToEndAsync(cancellationToken);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private sealed record CreateReportResult(string ReportId);

    public sealed record ReportStatus(
        string ReportId,
        string ReportType,
        string ProcessingStatus,
        string? ReportDocumentId);

    public sealed record ReportDocument(
        string ReportDocumentId,
        string Url,
        string? CompressionAlgorithm);
}
