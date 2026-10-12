using System.Net;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.SpApi;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Tests.SpApi;

public sealed class AwdGatewayTests : IDisposable
{
    private const string Page1 = """
        {"inventory":[
          {"sku":"AWD-1","totalOnhandQuantity":120,"totalInboundQuantity":30,"inventoryDetails":{"availableDistributableQuantity":100,"reservedDistributableQuantity":20,"replenishmentQuantity":12}},
          {"sku":"AWD-2","totalOnhandQuantity":5,"totalInboundQuantity":0},
          {"sku":"","totalOnhandQuantity":9}
        ],"nextToken":"abc/+="}
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

    private SpApiAwdGateway CreateGateway() =>
        new(new AwdApiClient(new HttpClient(_handler), Options.Create(new SpApiOptions())));

    [Fact]
    public async Task ListInventoryPageAsync_FirstPage_TagsOperationAndRegionAndMapsDetails()
    {
        _handler.Reply(HttpStatusCode.OK, Page1);

        var page = await CreateGateway().ListInventoryPageAsync(UnitedStates, nextToken: null, CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("https://sellingpartnerapi-na.amazon.com/awd/2024-05-09/inventory?details=SHOW&sortOrder=ASCENDING&maxResults=200", request.RequestUri!.ToString());
        Assert.True(request.Options.TryGetValue(SpApiOperation.OptionKey, out var operation));
        Assert.Equal(SpApiOperation.ListAwdInventory, operation);
        Assert.True(request.Options.TryGetValue(SpApiOperation.RegionKey, out var region));
        Assert.Equal(AmazonRegion.NorthAmerica, region);

        // Entries without a SKU are dropped; missing details count as zero.
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(new(Sku: "AWD-1", OnHand: 120, Inbound: 30, AvailableDistributable: 100, ReservedDistributable: 20, Replenishment: 12), page.Items[0]);
        Assert.Equal(new(Sku: "AWD-2", OnHand: 5, Inbound: 0, AvailableDistributable: 0, ReservedDistributable: 0, Replenishment: 0), page.Items[1]);
        Assert.Equal("abc/+=", page.NextToken);
        Assert.Equal(Page1, page.RawJson);
    }

    [Fact]
    public async Task ListInventoryPageAsync_NextPageInEurope_EscapesTokenAndUsesEuropeEndpoint()
    {
        _handler.Reply(HttpStatusCode.OK, """{"inventory":[]}""");

        var page = await CreateGateway().ListInventoryPageAsync(UnitedKingdom, "abc/+=", CancellationToken.None);

        var uri = Assert.Single(_handler.Requests).RequestUri!;
        Assert.Equal("sellingpartnerapi-eu.amazon.com", uri.Host);
        Assert.EndsWith("&nextToken=abc%2F%2B%3D", uri.Query, StringComparison.Ordinal);
        Assert.Empty(page.Items);
        Assert.Null(page.NextToken);
    }

    [Fact]
    public async Task ListInventoryPageAsync_ErrorStatus_ThrowsWithAmazonsMessage()
    {
        _handler.Reply(HttpStatusCode.Forbidden, """{"errors":[{"code":"Unauthorized","message":"Access to requested resource is denied."}]}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => CreateGateway().ListInventoryPageAsync(UnitedStates, null, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Contains("awd.listInventory", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Access to requested resource is denied.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulatedGateway_PagesThroughTheSameSkusEveryTime()
    {
        var gateway = new SimulatedAwdGateway();

        async Task<List<string>> ListAllAsync()
        {
            var skus = new List<string>();
            string? token = null;
            do
            {
                var page = await gateway.ListInventoryPageAsync(UnitedStates, token, CancellationToken.None);
                Assert.InRange(page.Items.Count, 1, SimulatedAwdGateway.PageSize);
                skus.AddRange(page.Items.Select(i => i.Sku));
                token = page.NextToken;
            }
            while (token is not null);

            return skus;
        }

        var first = await ListAllAsync();

        Assert.True(first.Count > SimulatedAwdGateway.PageSize, "The simulator should span several pages.");
        Assert.All(first, sku => Assert.Contains(SimulatedReportsGateway.Catalog, p => p.Sku == sku));
        Assert.Equal(first, await ListAllAsync());
    }
}
