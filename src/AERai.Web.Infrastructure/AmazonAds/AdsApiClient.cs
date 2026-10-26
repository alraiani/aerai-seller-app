using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>
/// Typed client for the Amazon Ads API: advertising profiles (<c>/v2/profiles</c>) and asynchronous reports
/// (reporting v3, <c>/reporting/reports</c>). Every call is tagged with its operation, region, and (for
/// reports) profile, and sent through <see cref="AdsApiPipelineHandler"/>.
/// </summary>
/// <param name="http">HttpClient with the Ads pipeline handler.</param>
/// <param name="httpClientFactory">Factory for the separate, token-less download client.</param>
/// <param name="options">Ads settings (regional hosts).</param>
internal sealed partial class AdsApiClient(HttpClient http, IHttpClientFactory httpClientFactory, IOptions<AmazonAdsOptions> options)
{
    /// <summary>
    /// Named client for report downloads. Reports are served from pre-signed S3 URLs, so this client
    /// never carries the Ads access token.
    /// </summary>
    public const string DownloadHttpClientName = "AdsReports";

    private const string CreateReportMediaType = "application/vnd.createasyncreportrequest.v3+json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Lists the advertising profiles the authorization can reach in a region.</summary>
    /// <param name="region">Region whose host is asked.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profiles.</returns>
    public async Task<IReadOnlyList<AdsProfile>> ListProfilesAsync(AmazonRegion region, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, region, "v2/profiles", AdsApiOperation.ListProfiles, profileId: null);
        var body = await SendAsync(request, AdsApiOperation.ListProfiles, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<List<AdsProfile>>(body, JsonOptions) ?? [];
    }

    /// <summary>Requests a Sponsored Products advertised product report: daily spend per SKU and campaign.</summary>
    /// <param name="region">Region of the profile.</param>
    /// <param name="profileId">Advertising profile id.</param>
    /// <param name="start">First day.</param>
    /// <param name="end">Last day (inclusive).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Amazon's report id.</returns>
    /// <remarks>
    /// Amazon refuses an identical request while the first is still being generated (HTTP 425) and names the
    /// report it is already making; that report is taken instead, so a retried request never fails the run.
    /// </remarks>
    public async Task<string> CreateSpendReportAsync(AmazonRegion region, string profileId, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var payload = new
        {
            name = string.Create(CultureInfo.InvariantCulture, $"AERai SP spend {start:yyyy-MM-dd} to {end:yyyy-MM-dd}"),
            startDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            endDate = end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            configuration = new
            {
                adProduct = "SPONSORED_PRODUCTS",
                groupBy = new[] { "advertiser" },
                columns = AdSpendReportParser.Columns,
                reportTypeId = "spAdvertisedProduct",
                timeUnit = "DAILY",
                format = "GZIP_JSON",
            },
        };

        using var request = Request(HttpMethod.Post, region, "reporting/reports", AdsApiOperation.CreateReport, profileId);
        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(CreateReportMediaType);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == (HttpStatusCode)425 && DuplicateOf(body) is { } existing)
        {
            return existing;
        }

        EnsureSuccess(response, body, AdsApiOperation.CreateReport);
        var report = JsonSerializer.Deserialize<ReportResponse>(body, JsonOptions);
        return report?.ReportId ?? throw new InvalidOperationException("Amazon Ads accepted the report request but returned no report id.");
    }

    /// <summary>Gets a report's status and, once completed, its download URL.</summary>
    /// <param name="region">Region of the profile.</param>
    /// <param name="profileId">Advertising profile id the report was requested under.</param>
    /// <param name="reportId">Report id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report.</returns>
    public async Task<ReportResponse> GetReportAsync(AmazonRegion region, string profileId, string reportId, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, region, $"reporting/reports/{Uri.EscapeDataString(reportId)}", AdsApiOperation.GetReport, profileId);
        var body = await SendAsync(request, AdsApiOperation.GetReport, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ReportResponse>(body, JsonOptions)
            ?? throw new InvalidOperationException($"Amazon Ads returned an empty status for report {reportId}.");
    }

    /// <summary>Downloads a completed report from its pre-signed URL, without the Ads token.</summary>
    /// <param name="url">The report's download URL.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The bytes as served (gzip); the caller disposes the stream.</returns>
    public async Task<Stream> DownloadAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        var response = await httpClientFactory.CreateClient(DownloadHttpClientName)
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // The URL is pre-signed, so it is never put in the message.
            var status = (int)response.StatusCode;
            response.Dispose();
            throw new HttpRequestException($"Downloading the Amazon Ads report failed with HTTP {status}.", null, (HttpStatusCode)status);
        }

        return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The report id Amazon names in a "duplicate request" (425) response, if any.</summary>
    /// <param name="body">The response body.</param>
    /// <returns>The report id, or <see langword="null"/>.</returns>
    internal static string? DuplicateOf(string body)
    {
        var match = DuplicateReportId().Match(body ?? string.Empty);
        return match.Success ? match.Groups["id"].Value : null;
    }

    private HttpRequestMessage Request(HttpMethod method, AmazonRegion region, string path, string operation, string? profileId)
    {
        var request = new HttpRequestMessage(method, new Uri(options.Value.EndpointFor(region), path));
        request.Options.Set(AdsApiOperation.OptionKey, operation);
        request.Options.Set(AdsApiOperation.RegionKey, region);
        if (profileId is not null)
        {
            request.Options.Set(AdsApiOperation.ProfileKey, profileId);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private async Task<string> SendAsync(HttpRequestMessage request, string operation, CancellationToken cancellationToken)
    {
        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        EnsureSuccess(response, body, operation);
        return body;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string body, string operation)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // Ads API error bodies ({"code","details"}) contain no secrets and help diagnose the call.
        throw new HttpRequestException(
            $"Amazon Ads {operation} failed with HTTP {(int)response.StatusCode}: {(body.Length <= 500 ? body : body[..500])}",
            null,
            response.StatusCode);
    }

    [GeneratedRegex(@"duplicate of\s*:?\s*(?<id>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})", RegexOptions.IgnoreCase)]
    private static partial Regex DuplicateReportId();
}

/// <summary>An advertising profile: one advertiser account in one marketplace.</summary>
/// <param name="ProfileId">Profile id, sent as the scope of every call for it.</param>
/// <param name="CountryCode">Country code (US, CA, UK).</param>
/// <param name="CurrencyCode">The account's currency.</param>
/// <param name="AccountInfo">The account behind the profile.</param>
internal sealed record AdsProfile(long ProfileId, string? CountryCode, string? CurrencyCode, AdsAccountInfo? AccountInfo);

/// <summary>The account behind an advertising profile.</summary>
/// <param name="MarketplaceStringId">SP-API marketplace id (e.g. <c>ATVPDKIKX0DER</c>).</param>
/// <param name="Type">Account type: <c>seller</c>, <c>vendor</c>, or <c>agency</c>.</param>
internal sealed record AdsAccountInfo(string? MarketplaceStringId, string? Type);

/// <summary>A reporting v3 report.</summary>
/// <param name="ReportId">Report id.</param>
/// <param name="Status"><c>PENDING</c>, <c>PROCESSING</c>, <c>COMPLETED</c>, or <c>FAILED</c>.</param>
/// <param name="Url">Pre-signed download URL once completed.</param>
/// <param name="FailureReason">Amazon's reason when failed.</param>
internal sealed record ReportResponse(string? ReportId, string? Status, Uri? Url, string? FailureReason);
