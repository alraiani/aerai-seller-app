using System.Net;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.AmazonAds;
using AERai.Web.Infrastructure.Tests.SpApi;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Infrastructure.Tests.AmazonAds;

/// <summary>
/// Tests the Amazon Ads choke point (token, client id and scope headers, refresh, retries) and the gateway
/// (profile lookup, report requests, downloads) against stub HTTP handlers — no network, no credentials.
/// </summary>
public sealed class AdsApiTests : IDisposable
{
    private const string TokenJson = """{"access_token":"ads-tok","expires_in":3600,"token_type":"bearer"}""";

    private const string Profiles = """
        [{"profileId":900,"countryCode":"US","currencyCode":"USD","accountInfo":{"marketplaceStringId":"ATVPDKIKX0DER","type":"vendor"}},
         {"profileId":111,"countryCode":"US","currencyCode":"USD","accountInfo":{"marketplaceStringId":"ATVPDKIKX0DER","type":"seller"}},
         {"profileId":222,"countryCode":"CA","currencyCode":"CAD","accountInfo":{"marketplaceStringId":"A2EUQ1WTGCTBG2","type":"seller"}}]
        """;

    private static readonly Marketplace UnitedStates = new()
    {
        MarketplaceId = MarketplaceIds.UnitedStates, Code = "US", Name = "United States", Region = AmazonRegion.NorthAmerica,
        Currency = "USD", TimeZoneId = "America/New_York", SalesChannel = "Amazon.com", IsActive = true,
    };

    private static readonly Marketplace UnitedKingdom = new()
    {
        MarketplaceId = MarketplaceIds.UnitedKingdom, Code = "UK", Name = "United Kingdom", Region = AmazonRegion.Europe,
        Currency = "GBP", TimeZoneId = "Europe/London", SalesChannel = "Amazon.co.uk", IsActive = true,
    };

    private readonly StubHttpHandler _lwa = new();
    private readonly StubHttpHandler _api = new();
    private readonly StubHttpHandler _downloads = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly AdsTokenProvider _tokens;
    private readonly IOptions<AmazonAdsOptions> _options = Options.Create(new AmazonAdsOptions
    {
        Mode = AmazonAdsMode.Live,
        ClientId = "ads-client",
        ClientSecret = "secret",
        RefreshToken = "refresh",
        MaxRetries = 3,

        // Zero delays keep retry tests instant; the backoff math is the same.
        RetryBaseDelay = TimeSpan.Zero,
        MaxRetryDelay = TimeSpan.Zero,
    });

    public AdsApiTests()
    {
        _tokens = new AdsTokenProvider(
            new StubHttpClientFactory(new() { [AdsTokenProvider.HttpClientName] = _lwa }), _options, _clock, NullLogger<AdsTokenProvider>.Instance);
    }

    public void Dispose()
    {
        _tokens.Dispose();
        _lwa.Dispose();
        _api.Dispose();
        _downloads.Dispose();
    }

    private AdsApiGateway CreateGateway()
    {
        var pipeline = new AdsApiPipelineHandler(_tokens, _options, _clock, NullLogger<AdsApiPipelineHandler>.Instance) { InnerHandler = _api };
        var client = new AdsApiClient(
            new HttpClient(pipeline), new StubHttpClientFactory(new() { [AdsApiClient.DownloadHttpClientName] = _downloads }), _options);
        return new AdsApiGateway(client, new AdsProfileCache(_clock));
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? values.Single() : null;

    [Fact]
    public async Task RequestSpendReportAsync_UsesTheMarketplacesSellerProfileAndAsksForDailySpendPerSku()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, Profiles).Reply(HttpStatusCode.OK, """{"reportId":"r-1","status":"PENDING"}""");

        var reportId = await CreateGateway().RequestSpendReportAsync(UnitedStates, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), CancellationToken.None);

        Assert.Equal("r-1", reportId);
        Assert.Equal("https://advertising-api.amazon.com/v2/profiles", _api.Requests[0].RequestUri!.ToString());
        Assert.Null(Header(_api.Requests[0], "Amazon-Advertising-API-Scope"));

        var create = _api.Requests[1];
        Assert.Equal((HttpMethod.Post, "https://advertising-api.amazon.com/reporting/reports"), (create.Method, create.RequestUri!.ToString()));
        Assert.Equal("Bearer ads-tok", create.Headers.Authorization!.ToString());
        Assert.Equal("ads-client", Header(create, "Amazon-Advertising-API-ClientId"));
        Assert.Equal("111", Header(create, "Amazon-Advertising-API-Scope"));
        Assert.Equal("application/vnd.createasyncreportrequest.v3+json", create.Content!.Headers.ContentType!.MediaType);
        var body = _api.Bodies[1]!;
        Assert.Contains("\"reportTypeId\":\"spAdvertisedProduct\"", body, StringComparison.Ordinal);
        Assert.Contains("\"startDate\":\"2026-09-01\"", body, StringComparison.Ordinal);
        Assert.Contains("\"endDate\":\"2026-09-30\"", body, StringComparison.Ordinal);
        Assert.Contains("\"timeUnit\":\"DAILY\"", body, StringComparison.Ordinal);
        Assert.Contains("advertisedSku", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RequestSpendReportAsync_DuplicateRequest_TakesTheReportAmazonIsAlreadyMaking()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, Profiles)
            .Reply((HttpStatusCode)425, """{"code":"425","detail":"The Request is a duplicate of : 0c7a2f1e-1234-4abc-9def-0123456789ab"}""");

        var reportId = await CreateGateway().RequestSpendReportAsync(UnitedStates, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), CancellationToken.None);

        Assert.Equal("0c7a2f1e-1234-4abc-9def-0123456789ab", reportId);
    }

    [Fact]
    public async Task RequestSpendReportAsync_NoProfileForTheMarketplace_ListsAgainThenFailsNamingIt()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, Profiles).Reply(HttpStatusCode.OK, Profiles);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            CreateGateway().RequestSpendReportAsync(UnitedKingdom, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), CancellationToken.None));

        Assert.Contains("No Amazon Ads profile for United Kingdom", ex.Message, StringComparison.Ordinal);
        Assert.All(_api.Requests, r => Assert.Equal("https://advertising-api-eu.amazon.com/v2/profiles", r.RequestUri!.ToString()));
        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task Pipeline_Unauthorized_RefreshesTheTokenOnceAndRetries()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson).Reply(HttpStatusCode.OK, TokenJson.Replace("ads-tok", "ads-tok-2", StringComparison.Ordinal));
        _api.Reply(HttpStatusCode.Unauthorized).Reply(HttpStatusCode.OK, Profiles).Reply(HttpStatusCode.OK, """{"reportId":"r-2","status":"PENDING"}""");

        var reportId = await CreateGateway().RequestSpendReportAsync(UnitedStates, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), CancellationToken.None);

        Assert.Equal("r-2", reportId);
        Assert.Equal(2, _lwa.Requests.Count);
        Assert.Equal("Bearer ads-tok-2", _api.Requests[^1].Headers.Authorization!.ToString());
    }

    [Fact]
    public async Task Pipeline_DroppedConnectionOnCreateReport_IsNotRetried()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, Profiles).Fail(HttpRequestError.ResponseEnded);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateGateway().RequestSpendReportAsync(UnitedStates, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), CancellationToken.None));

        Assert.Equal(2, _api.Requests.Count);
    }

    [Fact]
    public async Task GetReportStatusAndOpen_CompletedReport_DownloadsFromTheLinkWithoutTheToken()
    {
        const string Completed = """{"reportId":"r-1","status":"COMPLETED","url":"https://offline-report-storage.s3.amazonaws.com/r-1.json.gz?sig=x"}""";
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, Profiles).Reply(HttpStatusCode.OK, Completed).Reply(HttpStatusCode.OK, Completed);
        _downloads.Reply(HttpStatusCode.OK, "[]");
        var gateway = CreateGateway();

        var status = await gateway.GetReportStatusAsync(UnitedStates, "r-1", CancellationToken.None);
        await using var report = await gateway.OpenReportAsync(UnitedStates, "r-1", CancellationToken.None);

        Assert.Equal(AdsReportState.Completed, status.State);
        Assert.Equal("https://advertising-api.amazon.com/reporting/reports/r-1", _api.Requests[1].RequestUri!.ToString());
        Assert.Equal("111", Header(_api.Requests[1], "Amazon-Advertising-API-Scope"));
        var download = Assert.Single(_downloads.Requests);
        Assert.Null(download.Headers.Authorization);
        Assert.Equal("[]", await new StreamReader(report).ReadToEndAsync());
    }

    [Theory]
    [InlineData("PENDING", AdsReportState.Pending)]
    [InlineData("PROCESSING", AdsReportState.Pending)]
    [InlineData("FAILED", AdsReportState.Failed)]
    public async Task GetReportStatusAsync_MapsAmazonsStatus(string status, AdsReportState expected)
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, Profiles).Reply(HttpStatusCode.OK, $$"""{"reportId":"r-1","status":"{{status}}","failureReason":"why"}""");

        var result = await CreateGateway().GetReportStatusAsync(UnitedStates, "r-1", CancellationToken.None);

        Assert.Equal(expected, result.State);
    }

    [Fact]
    public async Task GetReportStatusAsync_ErrorStatus_ThrowsWithAmazonsMessage()
    {
        _lwa.Reply(HttpStatusCode.OK, TokenJson);
        _api.Reply(HttpStatusCode.OK, Profiles).Reply(HttpStatusCode.BadRequest, """{"code":"400","detail":"Invalid report id"}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => CreateGateway().GetReportStatusAsync(UnitedStates, "nope", CancellationToken.None));

        Assert.Contains("Invalid report id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulatedGateway_ReportIsReadyAtTheSecondCheckAndParses()
    {
        var gateway = new SimulatedAdsGateway();
        var reportId = await gateway.RequestSpendReportAsync(UnitedStates, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 3), CancellationToken.None);

        Assert.Equal(AdsReportState.Pending, (await gateway.GetReportStatusAsync(UnitedStates, reportId, CancellationToken.None)).State);
        Assert.Equal(AdsReportState.Completed, (await gateway.GetReportStatusAsync(UnitedStates, reportId, CancellationToken.None)).State);
        await using var report = await gateway.OpenReportAsync(UnitedStates, reportId, CancellationToken.None);
        var rows = await AdSpendReportParser.ParseAsync(report, CancellationToken.None);

        // Four SKUs in two campaigns for three days.
        Assert.Equal(24, rows.Count);
        Assert.All(rows, r => Assert.Equal("USD", r.Currency));
    }
}
