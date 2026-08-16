using System.Net;
using System.Text;
using AERai.Seller.SpApiClient.CatalogItems;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AERai.Seller.SpApiClient.Tests.CatalogItems;

public class CatalogItemsApiClientTests
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

    private static (CatalogItemsApiClient Client, FakeHttpMessageHandler Handler) CreateClient(string responseJson)
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

        return (new CatalogItemsApiClient(pipeline, credentialStore), apiHandler);
    }

    [Fact]
    public async Task SearchCatalogItemsAsync_MapsParentAsinTitleBrandAndMainImage()
    {
        const string responseJson = """
        {
          "items": [
            {
              "asin": "B0CHILD1",
              "summaries": [{ "itemName": "Blue Widget - Large", "brandName": "Acme" }],
              "images": [{ "images": [
                { "variant": "SWATCH", "link": "https://example.com/swatch.jpg" },
                { "variant": "MAIN", "link": "https://example.com/main.jpg" }
              ] }],
              "relationships": [{ "relationships": [
                { "parentAsins": ["B0PARENT1"], "type": "VARIATION" }
              ] }]
            }
          ]
        }
        """;
        var (client, handler) = CreateClient(responseJson);

        var results = await client.SearchCatalogItemsAsync(["B0CHILD1"], CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("B0CHILD1", result.Asin);
        Assert.Equal("B0PARENT1", result.ParentAsin);
        Assert.Equal("Blue Widget - Large", result.Title);
        Assert.Equal("Acme", result.Brand);
        Assert.Equal("https://example.com/main.jpg", result.ImageUrl);
        Assert.Contains("identifiers=B0CHILD1", handler.LastRequest!.RequestUri!.Query);
    }

    [Fact]
    public async Task SearchCatalogItemsAsync_StandaloneAsin_HasNullParentAsin()
    {
        const string responseJson = """
        { "items": [ { "asin": "B0STANDALONE", "summaries": [{ "itemName": "Standalone Item" }] } ] }
        """;
        var (client, _) = CreateClient(responseJson);

        var results = await client.SearchCatalogItemsAsync(["B0STANDALONE"], CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Null(result.ParentAsin);
    }

    [Fact]
    public async Task SearchCatalogItemsAsync_EmptyInput_ReturnsEmptyWithoutCallingApi()
    {
        var (client, handler) = CreateClient("""{ "items": [] }""");

        var results = await client.SearchCatalogItemsAsync([], CancellationToken.None);

        Assert.Empty(results);
        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task SearchCatalogItemsAsync_TooManyIdentifiers_Throws()
    {
        var (client, _) = CreateClient("""{ "items": [] }""");
        var tooMany = Enumerable.Range(0, CatalogItemsApiClient.MaxIdentifiersPerRequest + 1)
            .Select(i => $"B0{i:D8}")
            .ToList();

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.SearchCatalogItemsAsync(tooMany, CancellationToken.None));
    }
}
