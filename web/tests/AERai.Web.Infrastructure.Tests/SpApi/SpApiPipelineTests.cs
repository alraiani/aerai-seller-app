using System.IO.Compression;
using System.Net;
using System.Text;
using AERai.Web.Infrastructure.SpApi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Infrastructure.Tests.SpApi;

/// <summary>
/// Tests the SP-API choke point (LWA tokens, retries, token refresh) and the Reports client against
/// stub HTTP handlers — no network, no credentials.
/// </summary>
public sealed class SpApiPipelineTests : IDisposable
{
    private const string TokenJson = """{"access_token":"tok-1","expires_in":3600,"token_type":"bearer"}""";

    private readonly StubHttpHandler _lwa = new();
    private readonly StubHttpHandler _api = new();
    private readonly StubHttpHandler _documents = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly SpApiRateLimiter _limiter = new();
    private readonly LwaTokenProvider _tokens;
    private readonly IOptions<SpApiOptions> _options = Options.Create(new SpApiOptions
    {
        Mode = SpApiMode.Live,
        ClientId = "client",
        ClientSecret = "secret",
        RefreshToken = "refresh",
        MaxRetries = 3,

        // Zero delays keep retry tests instant; the backoff math is the same.
        RetryBaseDelay = TimeSpan.Zero,
        MaxRetryDelay = TimeSpan.Zero,
    });

    public SpApiPipelineTests()
    {
        var factory = new StubHttpClientFactory(new()
        {
            [LwaTokenProvider.HttpClientName] = _lwa,
            [ReportsApiClient.DocumentHttpClientName] = _documents,
        });
        _tokens = new LwaTokenProvider(factory, _options, _clock, NullLogger<LwaTokenProvider>.Instance);
    }

    public void Dispose()
    {
        _tokens.Dispose();
        _limiter.Dispose();
        _lwa.Dispose();
        _api.Dispose();
        _documents.Dispose();
    }

    private ReportsApiClient CreateClient()
    {
        var pipeline = new SpApiPipelineHandler(_tokens, _limiter, _options, _clock, NullLogger<SpApiPipelineHandler>.Instance) { InnerHandler = _api };
        var http = new HttpClient(pipeline) { BaseAddress = _options.Value.Endpoint };
        var factory = new StubHttpClientFactory(new() { [ReportsApiClient.DocumentHttpClientName] = _documents });
        return new ReportsApiClient(http, factory, _options);
    }

    [Fact]
    public async Task CreateReport_AttachesAccessTokenAndSendsReportType()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.Accepted, """{"reportId":"123"}""");

        var reportId = await CreateClient().CreateReportAsync("GET_FBA_MYI_ALL_INVENTORY_DATA", null, null, CancellationToken.None);

        Assert.Equal("123", reportId);
        Assert.Equal("tok-1", Assert.Single(_api.AccessTokensSeen));
        var body = _api.Bodies[0]!;
        Assert.Contains("\"reportType\":\"GET_FBA_MYI_ALL_INVENTORY_DATA\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain("dataStartTime", body, StringComparison.Ordinal); // Omitted for snapshot reports.
    }

    [Fact]
    public async Task Throttled429_IsRetriedThenSucceeds()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.TooManyRequests).Reply(HttpStatusCode.ServiceUnavailable)
            .Reply(HttpStatusCode.OK, """{"reportId":"1","reportType":"X","processingStatus":"DONE","reportDocumentId":"D","createdTime":"2026-10-02T00:00:00Z"}""");

        var report = await CreateClient().GetReportAsync("1", CancellationToken.None);

        Assert.Equal("DONE", report.ProcessingStatus);
        Assert.Equal(3, _api.Requests.Count);
        Assert.Single(_lwa.Requests); // The cached token is reused across retries.
    }

    [Fact]
    public async Task RetriesExhausted_SurfacesTheError()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        for (var i = 0; i < 4; i++)
        {
            _api.Reply(HttpStatusCode.TooManyRequests);
        }

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient().GetReportAsync("1", CancellationToken.None));

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Equal(4, _api.Requests.Count); // 1 attempt + MaxRetries (3).
    }

    [Fact]
    public async Task ClientError400_IsNotRetried()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.BadRequest, """{"errors":[{"code":"InvalidInput","message":"bad"}]}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => CreateClient().GetReportAsync("1", CancellationToken.None));

        Assert.Contains("InvalidInput", ex.Message, StringComparison.Ordinal);
        Assert.Single(_api.Requests);
    }

    [Fact]
    public async Task Unauthorized_RefreshesTokenOnceAndRetries()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson)
            .Reply(HttpStatusCode.OK, """{"access_token":"tok-2","expires_in":3600}""");
        _api.Reply(HttpStatusCode.Unauthorized).Reply(HttpStatusCode.Accepted, """{"reportId":"9"}""");

        await CreateClient().CreateReportAsync("X", null, null, CancellationToken.None);

        Assert.Equal(["tok-1", "tok-2"], _api.AccessTokensSeen);
    }

    [Fact]
    public async Task TokenProvider_CachesUntilNearExpiry()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson).Reply(HttpStatusCode.OK, """{"access_token":"tok-2","expires_in":3600}""");

        Assert.Equal("tok-1", await _tokens.GetAccessTokenAsync(CancellationToken.None));
        _clock.Advance(TimeSpan.FromMinutes(50));
        Assert.Equal("tok-1", await _tokens.GetAccessTokenAsync(CancellationToken.None));
        _clock.Advance(TimeSpan.FromMinutes(9)); // Within the 2-minute refresh margin.
        Assert.Equal("tok-2", await _tokens.GetAccessTokenAsync(CancellationToken.None));
    }

    [Fact]
    public async Task TokenProvider_RejectedRefresh_FailsWithoutLeakingCredentials()
    {
        _lwa.Reply(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _tokens.GetAccessTokenAsync(CancellationToken.None));

        Assert.DoesNotContain("secret", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("refresh", ex.Message.Replace("token refresh", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetDoneReports_FollowsNextTokenPages()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, """{"reports":[{"reportId":"A","reportType":"X","processingStatus":"DONE","reportDocumentId":"DA","createdTime":"2026-09-01T00:00:00Z"}],"nextToken":"page2"}""")
            .Reply(HttpStatusCode.OK, """{"reports":[{"reportId":"B","reportType":"X","processingStatus":"DONE","reportDocumentId":"DB","createdTime":"2026-09-15T00:00:00Z"}]}""");

        var reports = await CreateClient().GetDoneReportsAsync("X", new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), CancellationToken.None);

        Assert.Equal(["A", "B"], reports.Select(r => r.ReportId));
        Assert.Equal("?nextToken=page2", _api.Requests[1].RequestUri!.Query);
    }

    [Fact]
    public async Task OpenDocument_DecompressesGzipAndNeverSendsTokenToS3()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, """{"reportDocumentId":"D","url":"https://s3.example/doc","compressionAlgorithm":"GZIP"}""");

        using var compressed = new MemoryStream();
        await using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, leaveOpen: true))
        {
            await gzip.WriteAsync(Encoding.UTF8.GetBytes("sku\tafn-fulfillable-quantity\nA\t3\n"));
        }

        var gzipped = compressed.ToArray();
        _documents.Reply(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(gzipped) });

        await using var stream = await CreateClient().OpenDocumentAsync("D", CancellationToken.None);
        using var reader = new StreamReader(stream);

        Assert.Equal("sku\tafn-fulfillable-quantity\nA\t3\n", await reader.ReadToEndAsync());
        Assert.Null(Assert.Single(_documents.AccessTokensSeen));
    }
}
