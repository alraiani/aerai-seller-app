using System.Net;
using System.Text;
using AERai.Seller.SpApiClient.Awd;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AERai.Seller.SpApiClient.Tests.Awd;

public class AwdApiClientTests
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

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class SingleHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static (AwdApiClient Client, FakeHttpMessageHandler Handler) CreateClient(string responseJson)
    {
        var apiHandler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
        });
        var tokenHandler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
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

        return (new AwdApiClient(pipeline), apiHandler);
    }

    [Fact]
    public async Task ListInventoryAsync_MapsAllFields()
    {
        const string responseJson = """
        {
          "inventory": [
            {
              "sku": "SKU-1",
              "totalOnhandQuantity": 500,
              "totalInboundQuantity": 100,
              "inventoryDetails": {
                "availableDistributableQuantity": 400,
                "reservedDistributableQuantity": 50,
                "replenishmentQuantity": 25
              }
            }
          ],
          "nextToken": "token-123"
        }
        """;
        var (client, _) = CreateClient(responseJson);

        var result = await client.ListInventoryAsync(sku: null, nextToken: null, maxResults: 200, CancellationToken.None);

        var item = Assert.Single(result.Inventory);
        Assert.Equal("SKU-1", item.Sku);
        Assert.Equal(500, item.TotalOnhandQuantity);
        Assert.Equal(100, item.TotalInboundQuantity);
        Assert.Equal(400, item.AvailableDistributableQuantity);
        Assert.Equal(50, item.ReservedDistributableQuantity);
        Assert.Equal(25, item.ReplenishmentQuantity);
        Assert.Equal("token-123", result.NextToken);
    }

    [Fact]
    public async Task ListInventoryAsync_SkuAndNextTokenSupplied_IncludedInQuery()
    {
        var (client, handler) = CreateClient("""{ "inventory": [] }""");

        await client.ListInventoryAsync(sku: "SKU-42", nextToken: "abc", maxResults: 50, CancellationToken.None);

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.Contains("sku=SKU-42", query);
        Assert.Contains("nextToken=abc", query);
        Assert.Contains("maxResults=50", query);
    }

    [Fact]
    public async Task ListInventoryAsync_SkuAndNextTokenOmitted_NotInQuery()
    {
        var (client, handler) = CreateClient("""{ "inventory": [] }""");

        await client.ListInventoryAsync(sku: null, nextToken: null, maxResults: 200, CancellationToken.None);

        var query = handler.LastRequest!.RequestUri!.Query;
        Assert.DoesNotContain("sku=", query);
        Assert.DoesNotContain("nextToken=", query);
    }

    [Fact]
    public async Task ListInventoryAsync_EmptyInventory_ReturnsEmptyResultWithNullNextToken()
    {
        var (client, _) = CreateClient("""{ "inventory": [] }""");

        var result = await client.ListInventoryAsync(sku: null, nextToken: null, maxResults: 200, CancellationToken.None);

        Assert.Empty(result.Inventory);
        Assert.Null(result.NextToken);
    }
}
