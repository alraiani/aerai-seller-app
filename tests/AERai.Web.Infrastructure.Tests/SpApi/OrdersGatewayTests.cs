using System.Net;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.SpApi;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Tests.SpApi;

public sealed class OrdersGatewayTests : IDisposable
{
    private const string Page1 = """
        {"orders":[
          {"orderId":"111-0000001-0000001","createdTime":"2026-10-01T10:00:00Z","lastUpdatedTime":"2026-10-01T12:30:00Z",
           "salesChannel":{"marketplaceId":"ATVPDKIKX0DER"},
           "fulfillment":{"fulfillmentStatus":"PARTIALLY_SHIPPED"},
           "proceeds":{"grandTotal":{"amount":"59.97","currencyCode":"USD"}},
           "orderItems":[
             {"orderItemId":"1","quantityOrdered":2,"product":{"asin":"B000000001","title":"Widget","sellerSku":"W-1","price":{"unitPrice":{"amount":"19.99","currencyCode":"USD"}}}},
             {"orderItemId":"2","quantityOrdered":1,"product":{"asin":"B000000001","title":"Widget","sellerSku":"W-1","price":{"unitPrice":{"amount":"19.99","currencyCode":"USD"}}}},
             {"orderItemId":"3","quantityOrdered":1,"product":{"asin":"B000000009","title":"No SKU"}}
           ]},
          {"orderId":"111-0000001-0000002","createdTime":"2026-10-01T11:00:00Z","lastUpdatedTime":"2026-10-01T11:00:00Z",
           "fulfillment":{"fulfillmentStatus":"PENDING"},
           "orderItems":[{"orderItemId":"1","quantityOrdered":1,"product":{"sellerSku":"W-2"}}]},
          {"orderId":"","createdTime":"2026-10-01T11:00:00Z","lastUpdatedTime":"2026-10-01T11:00:00Z"}
        ],"pagination":{"nextToken":"abc/+="}}
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

    private readonly StubHttpHandler _handler = new();

    public void Dispose() => _handler.Dispose();

    private SpApiOrdersGateway CreateGateway() =>
        new(new OrdersApiClient(new HttpClient(_handler), Options.Create(new SpApiOptions())));

    [Fact]
    public async Task SearchOrdersPageAsync_ByLastUpdated_TagsOperationAndRegionAndMapsOrders()
    {
        _handler.Reply(HttpStatusCode.OK, Page1);
        var since = new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.FromHours(-4));

        var page = await CreateGateway().SearchOrdersPageAsync(UnitedStates, new AmazonOrdersFilter(null, null, since), nextToken: null, CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(
            "https://sellingpartnerapi-na.amazon.com/orders/2026-01-01/orders?marketplaceIds=ATVPDKIKX0DER&lastUpdatedAfter=2026-10-01T12%3A00%3A00Z",
            request.RequestUri!.ToString());
        Assert.True(request.Options.TryGetValue(SpApiOperation.OptionKey, out var operation));
        Assert.Equal(SpApiOperation.SearchOrders, operation);
        Assert.True(request.Options.TryGetValue(SpApiOperation.RegionKey, out var region));
        Assert.Equal(AmazonRegion.NorthAmerica, region);

        // Orders without an id are dropped.
        Assert.Equal(2, page.Orders.Count);

        // Same-SKU items are summed, items without a SKU dropped, and the status uses the report's spelling.
        var first = page.Orders[0];
        Assert.Equal("111-0000001-0000001", first.AmazonOrderId);
        Assert.Equal("Partially Shipped", first.Status);
        Assert.Equal("USD", first.Currency);
        Assert.Equal(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero), first.PurchaseDate);
        var line = Assert.Single(first.Lines);
        Assert.Equal(new AmazonOrderLine("W-1", "B000000001", "Widget", 3, 59.97m), line);

        // A pending order has no price yet and no proceeds: the line is free and the currency is the marketplace's.
        var pending = page.Orders[1];
        Assert.Equal("Pending", pending.Status);
        Assert.Equal("USD", pending.Currency);
        Assert.Equal(0m, Assert.Single(pending.Lines).ItemPrice);

        Assert.Equal("abc/+=", page.NextToken);
        Assert.Equal(Page1, page.RawJson);
    }

    [Fact]
    public async Task SearchOrdersPageAsync_CreatedRangeAndNextPage_ResendsTheFilterWithTheEscapedToken()
    {
        _handler.Reply(HttpStatusCode.OK, """{"orders":[]}""");
        var filter = new AmazonOrdersFilter(
            CreatedAfter: new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBefore: new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero),
            LastUpdatedAfter: null);

        var page = await CreateGateway().SearchOrdersPageAsync(UnitedKingdom, filter, "abc/+=", CancellationToken.None);

        var uri = Assert.Single(_handler.Requests).RequestUri!;
        Assert.Equal("sellingpartnerapi-eu.amazon.com", uri.Host);
        Assert.Contains("createdAfter=2026-09-01T00%3A00%3A00Z", uri.Query, StringComparison.Ordinal);
        Assert.Contains("createdBefore=2026-09-02T00%3A00%3A00Z", uri.Query, StringComparison.Ordinal);
        Assert.EndsWith("&paginationToken=abc%2F%2B%3D", uri.Query, StringComparison.Ordinal);
        Assert.Empty(page.Orders);
        Assert.Null(page.NextToken);
    }

    [Fact]
    public async Task SearchOrdersPageAsync_BothOrNeitherStartFilter_Throws()
    {
        var gateway = CreateGateway();
        var at = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<ArgumentException>(() => gateway.SearchOrdersPageAsync(UnitedStates, new AmazonOrdersFilter(at, null, at), null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => gateway.SearchOrdersPageAsync(UnitedStates, new AmazonOrdersFilter(null, null, null), null, CancellationToken.None));
        Assert.Empty(_handler.Requests);
    }

    [Fact]
    public async Task SearchOrdersPageAsync_ErrorStatus_ThrowsWithAmazonsMessage()
    {
        _handler.Reply(HttpStatusCode.BadRequest, """{"errors":[{"code":"InvalidInput","message":"createdBefore must be at least 2 minutes ago."}]}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateGateway().SearchOrdersPageAsync(UnitedStates, new AmazonOrdersFilter(DateTimeOffset.UtcNow, null, null), null, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("orders.searchOrders", ex.Message, StringComparison.Ordinal);
        Assert.Contains("at least 2 minutes", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("SHIPPED", "Shipped")]
    [InlineData("CANCELLED", "Cancelled")]
    [InlineData("PARTIALLY_SHIPPED", "Partially Shipped")]
    [InlineData("  unshipped ", "Unshipped")]
    [InlineData(null, "Unknown")]
    [InlineData("", "Unknown")]
    public void NormalizeStatus_UsesTheReportSpelling(string? input, string expected) =>
        Assert.Equal(expected, OrdersApiClient.NormalizeStatus(input));

    [Fact]
    public async Task SimulatedGateway_PagesThroughTheSameOrdersEveryTime()
    {
        var gateway = new SimulatedOrdersGateway();
        var filter = new AmazonOrdersFilter(CreatedAfter: DateTimeOffset.UtcNow.AddDays(-3), CreatedBefore: DateTimeOffset.UtcNow.AddMinutes(-3), LastUpdatedAfter: null);

        async Task<List<string>> ListAllAsync()
        {
            var ids = new List<string>();
            string? token = null;
            do
            {
                var page = await gateway.SearchOrdersPageAsync(UnitedStates, filter, token, CancellationToken.None);
                Assert.InRange(page.Orders.Count, 1, SimulatedOrdersGateway.PageSize);
                ids.AddRange(page.Orders.Select(o => o.AmazonOrderId));
                token = page.NextToken;
            }
            while (token is not null);

            return ids;
        }

        var first = await ListAllAsync();

        Assert.True(first.Count > SimulatedOrdersGateway.PageSize, "The simulator should span several pages.");
        Assert.Equal(first.Count, first.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first, await ListAllAsync());
    }
}
