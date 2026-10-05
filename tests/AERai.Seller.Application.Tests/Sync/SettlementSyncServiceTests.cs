using System.Net;
using System.Text;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Sync;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
using AERai.Seller.SpApiClient;
using AERai.Seller.SpApiClient.Reports;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AERai.Seller.Application.Tests.Sync;

public class SettlementSyncServiceTests
{
    private static readonly SpApiCredentials TestCredentials = new(
        ClientId: "test-client-id",
        ClientSecret: "test-client-secret",
        RefreshToken: "test-refresh-token",
        ApiHost: "https://sellingpartnerapi-test.amazon.com",
        MarketplaceId: "ATVPDKIKX0DER");

    private sealed class StaticCredentialStore(SpApiCredentials? credentials) : ICredentialStore
    {
        public Task<SpApiCredentials?> GetCredentialsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(credentials);
    }

    private sealed class RoutingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(responder(request));
    }

    private sealed class SingleHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class FakeSettlementRepository : ISettlementRepository
    {
        public List<(IReadOnlyList<SettlementReport> Settlements, IReadOnlyList<SettlementLineItem> LineItems)> UpsertCalls { get; } = [];

        public Task UpsertAsync(IEnumerable<SettlementReport> settlements, IEnumerable<SettlementLineItem> lineItems, CancellationToken cancellationToken = default)
        {
            UpsertCalls.Add((settlements.ToList(), lineItems.ToList()));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SettlementReport>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SettlementReport>>(UpsertCalls.SelectMany(c => c.Settlements).ToList());

        public Task<IReadOnlyList<SettlementLineItem>> GetLineItemsAsync(string settlementId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SettlementLineItem>>(
                UpsertCalls.SelectMany(c => c.LineItems).Where(l => l.SettlementId == settlementId).ToList());

        public Task<IReadOnlyList<(string AmountType, string AmountDescription)>> GetDistinctCategoriesAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeSyncMetadataRepository : ISyncMetadataRepository
    {
        public bool? LastSucceeded { get; private set; }
        public string? LastErrorMessage { get; private set; }

        public Task<SyncMetadata?> GetAsync(string syncJobName, CancellationToken cancellationToken = default)
            => Task.FromResult<SyncMetadata?>(null);
        public Task<IReadOnlyList<SyncMetadata>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<SyncMetadata>>([]);

        public Task RecordResultAsync(string syncJobName, bool succeeded, string? errorMessage, CancellationToken cancellationToken = default)
        {
            LastSucceeded = succeeded;
            LastErrorMessage = errorMessage;
            return Task.CompletedTask;
        }
    }

    private static ReportsApiClient CreateReportsApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var apiHandler = new RoutingHttpMessageHandler(responder);
        var tokenHandler = new RoutingHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"access_token":"test-access-token","token_type":"bearer","expires_in":3600}""",
                Encoding.UTF8, "application/json"),
        });

        var credentialStore = new StaticCredentialStore(TestCredentials);
        var tokenProvider = new LwaTokenProvider(
            new SingleHandlerHttpClientFactory(tokenHandler), credentialStore, NullLogger<LwaTokenProvider>.Instance);
        var pipeline = new SpApiRequestPipeline(
            new SingleHandlerHttpClientFactory(apiHandler), tokenProvider, credentialStore,
            new SpApiOperationRateLimiter(), NullLogger<SpApiRequestPipeline>.Instance);

        return new ReportsApiClient(pipeline, credentialStore, new SingleHandlerHttpClientFactory(apiHandler));
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task SyncAsync_RequestsPollsDownloadsAndUpsertsParsedSettlement()
    {
        const string tsv =
            "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\tmarketplace-name\torder-id\tsku\tamount-type\tamount-description\tamount\tposted-date\n" +
            "1000\t2026-07-01T00:00:00Z\t2026-07-14T00:00:00Z\t2026-07-16T00:00:00Z\t93.50\tUSD\tAmazon.com\t111-1\tWIDGET-1\tItemPrice\tPrincipal\t100.00\t2026-07-05T00:00:00Z\n";

        string? createReportBody = null;
        var reportsApiClient = CreateReportsApiClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/reports/2021-06-30/reports")
            {
                createReportBody = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                return JsonResponse("""{ "reportId": "report-1" }""");
            }
            if (path == "/reports/2021-06-30/reports/report-1")
            {
                return JsonResponse("""
                { "reportId": "report-1", "reportType": "GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2", "processingStatus": "DONE", "reportDocumentId": "doc-1" }
                """);
            }
            if (path == "/reports/2021-06-30/documents/doc-1")
            {
                return JsonResponse("""{ "reportDocumentId": "doc-1", "url": "https://test-download.example.com/report.tsv" }""");
            }
            if (request.RequestUri!.Host == "test-download.example.com")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(tsv, Encoding.UTF8, "text/tab-separated-values"),
                };
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var settlementRepository = new FakeSettlementRepository();
        var syncMetadataRepository = new FakeSyncMetadataRepository();
        var service = new SettlementSyncService(
            reportsApiClient, settlementRepository, syncMetadataRepository, TimeProvider.System, NullLogger<SettlementSyncService>.Instance);

        await service.SyncAsync();

        // The long (_V2) layout, not the wide non-_V2 one — see SettlementSyncService's doc comment.
        Assert.Contains("\"GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2\"", createReportBody);

        var call = Assert.Single(settlementRepository.UpsertCalls);
        var settlement = Assert.Single(call.Settlements);
        Assert.Equal("1000", settlement.SettlementId);
        Assert.Equal(93.50m, settlement.TotalAmount);
        var lineItem = Assert.Single(call.LineItems);
        Assert.Equal("ItemPrice", lineItem.AmountType);
        Assert.Equal(100.00m, lineItem.Amount);
        Assert.True(syncMetadataRepository.LastSucceeded);
    }

    [Fact]
    public async Task SyncAsync_ReportEndsFatal_RecordsFailure_WithoutUpserting()
    {
        var reportsApiClient = CreateReportsApiClient(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path == "/reports/2021-06-30/reports")
            {
                return JsonResponse("""{ "reportId": "report-1" }""");
            }
            if (path == "/reports/2021-06-30/reports/report-1")
            {
                return JsonResponse("""
                { "reportId": "report-1", "reportType": "GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2", "processingStatus": "FATAL", "reportDocumentId": null }
                """);
            }

            throw new InvalidOperationException($"Unexpected request: {request.RequestUri}");
        });

        var settlementRepository = new FakeSettlementRepository();
        var syncMetadataRepository = new FakeSyncMetadataRepository();
        var service = new SettlementSyncService(
            reportsApiClient, settlementRepository, syncMetadataRepository, TimeProvider.System, NullLogger<SettlementSyncService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SyncAsync());

        Assert.Empty(settlementRepository.UpsertCalls);
        Assert.False(syncMetadataRepository.LastSucceeded);
        Assert.NotNull(syncMetadataRepository.LastErrorMessage);
    }
}
