using System.Net;
using System.Text;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Sync;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
using AERai.Seller.SpApiClient;
using AERai.Seller.SpApiClient.CatalogItems;
using Microsoft.Extensions.Logging.Abstractions;

namespace AERai.Seller.Application.Tests.Sync;

public class CatalogSyncServiceTests
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

    private sealed class FakeProductRepository(IReadOnlyList<Product> products) : IProductRepository
    {
        public Task UpsertAsync(Product product, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(products);
    }

    private sealed class FakeOrderRepository(IReadOnlyList<OrderItem> items) : IOrderRepository
    {
        public Task<int> InsertNewOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Order>> GetOrdersPurchasedBetweenAsync(DateOnly startDateInclusive, DateOnly endDateInclusive, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<OrderItem>> GetAllOrderItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(items);
        public Task<DateOnly?> GetEarliestOrderDateAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeCatalogRepository : ICatalogRepository
    {
        public List<CatalogItem> UpsertedItems { get; private set; } = [];
        public List<CatalogParent> UpsertedParents { get; private set; } = [];

        public Task UpsertAsync(IEnumerable<CatalogItem> items, IEnumerable<CatalogParent> parents, CancellationToken cancellationToken = default)
        {
            UpsertedItems = items.ToList();
            UpsertedParents = parents.ToList();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<CatalogItem>> GetAllItemsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CatalogItem>>(UpsertedItems);

        public Task<IReadOnlyList<CatalogParent>> GetAllParentsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<CatalogParent>>(UpsertedParents);
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

    private static CatalogItemsApiClient CreateCatalogItemsApiClient(Func<HttpRequestMessage, HttpResponseMessage> responder)
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

        return new CatalogItemsApiClient(pipeline, credentialStore);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task SyncAsync_GathersAsinsFromProductsAndOrderItems_AndResolvesParentTitle()
    {
        var products = new List<Product> { new() { Sku = "SKU-A", Asin = "B0CHILD1" } };
        var orderItems = new List<OrderItem>
        {
            new() { Id = 1, AmazonOrderId = "O1", Sku = "SKU-B", Asin = "B0CHILD2" },
        };

        var catalogItemsApiClient = CreateCatalogItemsApiClient(request =>
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("B0PARENT1") && !query.Contains("B0CHILD"))
            {
                return JsonResponse("""
                { "items": [ { "asin": "B0PARENT1", "summaries": [{ "itemName": "Widget Family" }] } ] }
                """);
            }

            return JsonResponse("""
            {
              "items": [
                {
                  "asin": "B0CHILD1",
                  "summaries": [{ "itemName": "Widget - Red" }],
                  "relationships": [{ "relationships": [{ "parentAsins": ["B0PARENT1"], "type": "VARIATION" }] }]
                },
                {
                  "asin": "B0CHILD2",
                  "summaries": [{ "itemName": "Widget - Blue" }],
                  "relationships": [{ "relationships": [{ "parentAsins": ["B0PARENT1"], "type": "VARIATION" }] }]
                }
              ]
            }
            """);
        });

        var catalogRepository = new FakeCatalogRepository();
        var syncMetadataRepository = new FakeSyncMetadataRepository();

        var service = new CatalogSyncService(
            catalogItemsApiClient,
            new FakeProductRepository(products),
            new FakeOrderRepository(orderItems),
            catalogRepository,
            syncMetadataRepository,
            TimeProvider.System,
            NullLogger<CatalogSyncService>.Instance);

        await service.SyncAsync();

        Assert.Equal(2, catalogRepository.UpsertedItems.Count);
        var child1 = Assert.Single(catalogRepository.UpsertedItems, i => i.Asin == "B0CHILD1");
        Assert.Equal("SKU-A", child1.Sku);
        Assert.Equal("B0PARENT1", child1.ParentAsin);
        var child2 = Assert.Single(catalogRepository.UpsertedItems, i => i.Asin == "B0CHILD2");
        Assert.Equal("SKU-B", child2.Sku);

        var parent = Assert.Single(catalogRepository.UpsertedParents);
        Assert.Equal("B0PARENT1", parent.ParentAsin);
        Assert.Equal("Widget Family", parent.Title);

        Assert.True(syncMetadataRepository.LastSucceeded);
    }

    [Fact]
    public async Task SyncAsync_NoKnownAsins_UpsertsNothing_AndStillRecordsSuccess()
    {
        var catalogItemsApiClient = CreateCatalogItemsApiClient(
            _ => throw new InvalidOperationException("Should not call the API when there are no ASINs."));
        var catalogRepository = new FakeCatalogRepository();
        var syncMetadataRepository = new FakeSyncMetadataRepository();

        var service = new CatalogSyncService(
            catalogItemsApiClient,
            new FakeProductRepository([]),
            new FakeOrderRepository([]),
            catalogRepository,
            syncMetadataRepository,
            TimeProvider.System,
            NullLogger<CatalogSyncService>.Instance);

        await service.SyncAsync();

        Assert.Empty(catalogRepository.UpsertedItems);
        Assert.Empty(catalogRepository.UpsertedParents);
        Assert.True(syncMetadataRepository.LastSucceeded);
    }

    [Fact]
    public async Task SyncAsync_ApiFailure_RecordsFailureAndRethrows()
    {
        var products = new List<Product> { new() { Sku = "SKU-A", Asin = "B0CHILD1" } };
        var catalogItemsApiClient = CreateCatalogItemsApiClient(
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var syncMetadataRepository = new FakeSyncMetadataRepository();

        var service = new CatalogSyncService(
            catalogItemsApiClient,
            new FakeProductRepository(products),
            new FakeOrderRepository([]),
            new FakeCatalogRepository(),
            syncMetadataRepository,
            TimeProvider.System,
            NullLogger<CatalogSyncService>.Instance);

        await Assert.ThrowsAsync<SpApiException>(() => service.SyncAsync());

        Assert.False(syncMetadataRepository.LastSucceeded);
        Assert.NotNull(syncMetadataRepository.LastErrorMessage);
    }
}
