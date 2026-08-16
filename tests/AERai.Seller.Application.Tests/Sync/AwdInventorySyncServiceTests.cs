using System.Net;
using System.Text;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Sync;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
using AERai.Seller.SpApiClient;
using AERai.Seller.SpApiClient.Awd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AERai.Seller.Application.Tests.Sync;

public class AwdInventorySyncServiceTests
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

    private sealed class FakeAwdInventoryRepository : IAwdInventoryRepository
    {
        public List<List<AwdInventorySnapshot>> UpsertCalls { get; } = [];

        public Task UpsertSnapshotsAsync(IEnumerable<AwdInventorySnapshot> snapshots, CancellationToken cancellationToken = default)
        {
            UpsertCalls.Add(snapshots.ToList());
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AwdInventorySnapshot>> GetLatestSnapshotsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<AwdInventorySnapshot>>(UpsertCalls.SelectMany(c => c).ToList());
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

    private static AwdApiClient CreateAwdApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
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

        return new AwdApiClient(pipeline);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task SyncAsync_PagesThroughNextToken_UpsertingEachPageImmediately()
    {
        var awdApiClient = CreateAwdApiClient(request =>
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("nextToken="))
            {
                return JsonResponse("""{ "inventory": [ { "sku": "SKU-2", "totalOnhandQuantity": 20 } ] }""");
            }

            return JsonResponse("""
            { "inventory": [ { "sku": "SKU-1", "totalOnhandQuantity": 10 } ], "nextToken": "page2" }
            """);
        });

        var repository = new FakeAwdInventoryRepository();
        var syncMetadataRepository = new FakeSyncMetadataRepository();
        var service = new AwdInventorySyncService(
            awdApiClient, repository, syncMetadataRepository, TimeProvider.System, NullLogger<AwdInventorySyncService>.Instance);

        await service.SyncAsync();

        // Two separate upsert calls (one per page), not one call batched at the end.
        Assert.Equal(2, repository.UpsertCalls.Count);
        Assert.Equal("SKU-1", Assert.Single(repository.UpsertCalls[0]).Sku);
        Assert.Equal("SKU-2", Assert.Single(repository.UpsertCalls[1]).Sku);
        Assert.True(syncMetadataRepository.LastSucceeded);
    }

    [Fact]
    public async Task SyncAsync_ApiFailureMidPagination_KeepsEarlierPagesAndRecordsFailure()
    {
        var awdApiClient = CreateAwdApiClient(request =>
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("nextToken="))
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }

            return JsonResponse("""
            { "inventory": [ { "sku": "SKU-1", "totalOnhandQuantity": 10 } ], "nextToken": "page2" }
            """);
        });

        var repository = new FakeAwdInventoryRepository();
        var syncMetadataRepository = new FakeSyncMetadataRepository();
        var service = new AwdInventorySyncService(
            awdApiClient, repository, syncMetadataRepository, TimeProvider.System, NullLogger<AwdInventorySyncService>.Instance);

        await Assert.ThrowsAsync<SpApiException>(() => service.SyncAsync());

        // The first page's upsert already happened before the second page failed.
        var upserted = Assert.Single(repository.UpsertCalls);
        Assert.Equal("SKU-1", Assert.Single(upserted).Sku);
        Assert.False(syncMetadataRepository.LastSucceeded);
        Assert.NotNull(syncMetadataRepository.LastErrorMessage);
    }
}
