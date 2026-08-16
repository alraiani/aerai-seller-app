using System.Net;
using System.Text;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Sync;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
using AERai.Seller.SpApiClient;
using AERai.Seller.SpApiClient.Orders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AERai.Seller.Application.Tests.Sync;

public class OrderSyncServiceBackfillTests
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

    private sealed class FakeOrderRepository(CancellationTokenSource? cancelAfterFirstInsert = null) : IOrderRepository
    {
        public List<List<Order>> InsertCalls { get; } = [];

        public Task<int> InsertNewOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default)
        {
            var list = orders.ToList();
            InsertCalls.Add(list);
            if (InsertCalls.Count == 1 && cancelAfterFirstInsert is not null)
            {
                // Simulate the user clicking Cancel while page 2 would otherwise be fetched next —
                // throwing here (rather than relying on the real HttpClient/rate-limiter to notice
                // an already-cancelled token before a second network call) makes the test
                // deterministic and avoids ever attempting that second call at all.
                cancelAfterFirstInsert.Cancel();
                cancelAfterFirstInsert.Token.ThrowIfCancellationRequested();
            }

            return Task.FromResult(list.Count);
        }

        public Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Order>> GetOrdersPurchasedBetweenAsync(DateOnly startDateInclusive, DateOnly endDateInclusive, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<OrderItem>> GetAllOrderItemsAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
        public Task<DateOnly?> GetEarliestOrderDateAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private static OrdersApiClient CreateOrdersApiClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder, out List<Uri> capturedRequestUris)
    {
        var uris = new List<Uri>();
        capturedRequestUris = uris;

        var apiHandler = new RoutingHttpMessageHandler(request =>
        {
            uris.Add(request.RequestUri!);
            return responder(request);
        });
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

        return new OrdersApiClient(pipeline, credentialStore);
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    [Fact]
    public async Task BackfillLastNDaysAsync_UsesOneRangedWindow_NotADayLoop()
    {
        var ordersApiClient = CreateOrdersApiClient(request =>
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("paginationToken="))
            {
                return JsonResponse("""{ "orders": [] }""");
            }

            return JsonResponse("""
            {
              "orders": [ { "orderId": "ORDER-1", "createdTime": "2026-05-01T00:00:00Z", "lastUpdatedTime": "2026-05-01T00:00:00Z", "orderItems": [] } ],
              "pagination": { "nextToken": "page2" }
            }
            """);
        }, out var capturedRequestUris);

        var orderRepository = new FakeOrderRepository();
        var syncMetadataRepository = new FakeSyncMetadataRepository();
        var service = new OrderSyncService(
            ordersApiClient, orderRepository, syncMetadataRepository, TimeProvider.System, NullLogger<OrderSyncService>.Instance);

        await service.BackfillLastNDaysAsync(90);

        Assert.Equal(2, capturedRequestUris.Count);
        var firstPageQuery = capturedRequestUris[0].Query;
        var secondPageQuery = capturedRequestUris[1].Query;

        // Both pages must resend the identical createdAfter — a single 90-day window, not a
        // per-day loop that would re-derive a different createdAfter for each call.
        var createdAfterOnFirstPage = firstPageQuery.Split('&').Single(p => p.StartsWith("createdAfter="));
        Assert.Contains(createdAfterOnFirstPage, secondPageQuery);
        Assert.Contains("paginationToken=page2", secondPageQuery);
        Assert.True(syncMetadataRepository.LastSucceeded);
    }

    [Fact]
    public async Task BackfillLastNDaysAsync_UpsertsEachPageImmediately()
    {
        var ordersApiClient = CreateOrdersApiClient(request =>
        {
            var query = request.RequestUri!.Query;
            if (query.Contains("paginationToken="))
            {
                return JsonResponse("""
                { "orders": [ { "orderId": "ORDER-2", "createdTime": "2026-05-02T00:00:00Z", "lastUpdatedTime": "2026-05-02T00:00:00Z", "orderItems": [] } ] }
                """);
            }

            return JsonResponse("""
            {
              "orders": [ { "orderId": "ORDER-1", "createdTime": "2026-05-01T00:00:00Z", "lastUpdatedTime": "2026-05-01T00:00:00Z", "orderItems": [] } ],
              "pagination": { "nextToken": "page2" }
            }
            """);
        }, out _);

        var orderRepository = new FakeOrderRepository();
        var syncMetadataRepository = new FakeSyncMetadataRepository();
        var service = new OrderSyncService(
            ordersApiClient, orderRepository, syncMetadataRepository, TimeProvider.System, NullLogger<OrderSyncService>.Instance);

        var result = await service.BackfillLastNDaysAsync(90);

        Assert.Equal(2, orderRepository.InsertCalls.Count);
        Assert.Equal("ORDER-1", Assert.Single(orderRepository.InsertCalls[0]).AmazonOrderId);
        Assert.Equal("ORDER-2", Assert.Single(orderRepository.InsertCalls[1]).AmazonOrderId);
        Assert.Equal(2, result.PagesFetched);
        Assert.Equal(2, result.TotalSeen);
    }

    [Fact]
    public async Task BackfillLastNDaysAsync_CancelledMidPagination_KeepsEarlierPagePersisted()
    {
        using var cts = new CancellationTokenSource();
        var ordersApiClient = CreateOrdersApiClient(_ => JsonResponse("""
            {
              "orders": [ { "orderId": "ORDER-1", "createdTime": "2026-05-01T00:00:00Z", "lastUpdatedTime": "2026-05-01T00:00:00Z", "orderItems": [] } ],
              "pagination": { "nextToken": "page2" }
            }
            """), out _);

        // Cancellation fires as a side effect of the first page's upsert completing, simulating
        // the user clicking Cancel while a multi-page backfill is running.
        var orderRepository = new FakeOrderRepository(cts);
        var syncMetadataRepository = new FakeSyncMetadataRepository();
        var service = new OrderSyncService(
            ordersApiClient, orderRepository, syncMetadataRepository, TimeProvider.System, NullLogger<OrderSyncService>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.BackfillLastNDaysAsync(90, cancellationToken: cts.Token));

        // Only the first page was ever inserted — cancellation stopped the loop before page 2.
        Assert.Single(orderRepository.InsertCalls);
        // A user-cancelled partial pull is not recorded as a sync failure.
        Assert.Null(syncMetadataRepository.LastSucceeded);
    }
}
