using System.Net;
using AERai.Seller.SpApiClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AERai.Seller.SpApiClient.Tests;

public class SpApiRequestPipelineTests
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
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }

    private sealed class SingleHandlerHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private static (SpApiRequestPipeline Pipeline, FakeHttpMessageHandler Handler) CreatePipeline(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        Func<HttpRequestMessage, HttpResponseMessage>? tokenResponder = null)
    {
        var apiHandler = new FakeHttpMessageHandler(responder);
        var tokenHandler = new FakeHttpMessageHandler(tokenResponder ?? (_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"access_token":"test-access-token","token_type":"bearer","expires_in":3600}""",
                System.Text.Encoding.UTF8, "application/json"),
        }));

        var credentialStore = new StaticCredentialStore(TestCredentials);
        var tokenProvider = new LwaTokenProvider(
            new SingleHandlerHttpClientFactory(tokenHandler), credentialStore, NullLogger<LwaTokenProvider>.Instance);
        var rateLimiter = new SpApiOperationRateLimiter();
        var pipeline = new SpApiRequestPipeline(
            new SingleHandlerHttpClientFactory(apiHandler), tokenProvider, credentialStore, rateLimiter,
            NullLogger<SpApiRequestPipeline>.Instance);

        return (pipeline, apiHandler);
    }

    [Fact]
    public async Task SendAsync_ReturnsResponse_OnFirstSuccess()
    {
        var (pipeline, handler) = CreatePipeline(_ => new HttpResponseMessage(HttpStatusCode.OK));

        using var response = await pipeline.SendAsync(
            "Test.Operation", host => new HttpRequestMessage(HttpMethod.Get, $"{host}/ping"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_RetriesOn429_ThenSucceeds()
    {
        var callCount = 0;
        var (pipeline, handler) = CreatePipeline(_ =>
        {
            callCount++;
            return callCount < 3
                ? new HttpResponseMessage((HttpStatusCode)429)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var response = await pipeline.SendAsync(
            "Test.Operation", host => new HttpRequestMessage(HttpMethod.Get, $"{host}/ping"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task SendAsync_RetriesOn5xx_ThenSucceeds()
    {
        var callCount = 0;
        var (pipeline, _) = CreatePipeline(_ =>
        {
            callCount++;
            return callCount < 2
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var response = await pipeline.SendAsync(
            "Test.Operation", host => new HttpRequestMessage(HttpMethod.Get, $"{host}/ping"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SendAsync_ThrowsSpApiException_OnNonRetryable4xx()
    {
        var (pipeline, _) = CreatePipeline(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("""{"errors":[{"code":"InvalidInput"}]}"""),
        });

        var exception = await Assert.ThrowsAsync<SpApiException>(() => pipeline.SendAsync(
            "Test.Operation", host => new HttpRequestMessage(HttpMethod.Get, $"{host}/ping"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Contains("InvalidInput", exception.ResponseBody);
    }

    [Fact]
    public async Task SendAsync_RefreshesTokenOnce_On401()
    {
        var apiCallCount = 0;
        var (pipeline, apiHandler) = CreatePipeline(_ =>
        {
            apiCallCount++;
            return apiCallCount == 1
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var response = await pipeline.SendAsync(
            "Test.Operation", host => new HttpRequestMessage(HttpMethod.Get, $"{host}/ping"), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, apiHandler.CallCount);
    }

    [Fact]
    public async Task SendAsync_Throws_WhenNoCredentialsConfigured()
    {
        var credentialStore = new StaticCredentialStore(null);
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var tokenProvider = new LwaTokenProvider(
            new SingleHandlerHttpClientFactory(handler), credentialStore, NullLogger<LwaTokenProvider>.Instance);
        var pipeline = new SpApiRequestPipeline(
            new SingleHandlerHttpClientFactory(handler), tokenProvider, credentialStore,
            new SpApiOperationRateLimiter(), NullLogger<SpApiRequestPipeline>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.SendAsync(
            "Test.Operation", host => new HttpRequestMessage(HttpMethod.Get, $"{host}/ping"), CancellationToken.None));
    }
}
