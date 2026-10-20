using System.Net;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.SpApi;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Tests.SpApi;

public sealed class FinancesGatewayTests : IDisposable
{
    // The shape of Amazon's listTransactions response (Finances API v2024-06-19).
    private const string Page1 = """
        {"payload":{"nextToken":"tok/+=","transactions":[
          {"transactionId":"T-1","transactionType":"Shipment","transactionStatus":"DEFERRED","description":"Order Payment",
           "postedDate":"2026-10-15T08:00:00Z","totalAmount":{"currencyCode":"USD","currencyAmount":18.42},
           "relatedIdentifiers":[{"relatedIdentifierName":"SHIPMENT_ID","relatedIdentifierValue":"SH-1"},{"relatedIdentifierName":"ORDER_ID","relatedIdentifierValue":"113-1"}],
           "marketplaceDetails":{"marketplaceId":"ATVPDKIKX0DER","marketplaceName":"Amazon.com"},
           "contexts":[{"contextType":"ProductContext","asin":"B0"},{"contextType":"DeferredContext","deferralReason":"DD7","maturityDate":"2026-10-24T07:00:00Z"}]},
          {"transactionId":"T-2","transactionStatus":"RELEASED","totalAmount":{"currencyCode":"USD","currencyAmount":5}},
          {"transactionId":"","transactionStatus":"DEFERRED"}
        ]}}
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

    private static readonly DateTimeOffset PostedAfter = new(2026, 4, 24, 12, 0, 0, TimeSpan.Zero);

    private readonly StubHttpHandler _handler = new();

    public void Dispose() => _handler.Dispose();

    private SpApiFinancesGateway CreateGateway() =>
        new(new FinancesApiClient(new HttpClient(_handler), Options.Create(new SpApiOptions())));

    [Fact]
    public async Task ListTransactionsPageAsync_FirstPage_AsksForDeferredAndMapsTheDeferredContext()
    {
        _handler.Reply(HttpStatusCode.OK, Page1);

        var page = await CreateGateway().ListTransactionsPageAsync(UnitedStates, PostedAfter, nextToken: null, CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal(
            "https://sellingpartnerapi-na.amazon.com/finances/2024-06-19/transactions?postedAfter=2026-04-24T12%3A00%3A00Z&marketplaceId=ATVPDKIKX0DER&transactionStatus=DEFERRED",
            request.RequestUri!.ToString());
        Assert.True(request.Options.TryGetValue(SpApiOperation.OptionKey, out var operation));
        Assert.Equal(SpApiOperation.ListTransactions, operation);
        Assert.True(request.Options.TryGetValue(SpApiOperation.RegionKey, out var region));
        Assert.Equal(AmazonRegion.NorthAmerica, region);

        // Only still-deferred transactions with an id are kept; the order id and the deferral come from their own entries.
        var item = Assert.Single(page.Items);
        Assert.Equal(
            ("T-1", "113-1", "Shipment", "Order Payment", 18.42m, "USD", "DD7"),
            (item.TransactionId, item.AmazonOrderId, item.TransactionType, item.Description, item.Amount, item.Currency, item.DeferralReason));
        Assert.Equal(new DateTimeOffset(2026, 10, 24, 7, 0, 0, TimeSpan.Zero), item.MaturityDate);
        Assert.Equal(new DateTimeOffset(2026, 10, 15, 8, 0, 0, TimeSpan.Zero), item.PostedDate);
        Assert.Equal("tok/+=", page.NextToken);
        Assert.Equal(Page1, page.RawJson);
    }

    [Fact]
    public async Task ListTransactionsPageAsync_NextPageInEurope_KeepsTheArgumentsAndEscapesTheToken()
    {
        _handler.Reply(HttpStatusCode.OK, """{"payload":{"transactions":[]}}""");

        var page = await CreateGateway().ListTransactionsPageAsync(UnitedKingdom, PostedAfter, "tok/+=", CancellationToken.None);

        var uri = Assert.Single(_handler.Requests).RequestUri!;
        Assert.Equal("sellingpartnerapi-eu.amazon.com", uri.Host);
        Assert.Contains("marketplaceId=A1F83G8C2ARO7P&transactionStatus=DEFERRED", uri.Query, StringComparison.Ordinal);
        Assert.EndsWith("&nextToken=tok%2F%2B%3D", uri.Query, StringComparison.Ordinal);
        Assert.Empty(page.Items);
        Assert.Null(page.NextToken);
    }

    [Fact]
    public async Task ListTransactionsPageAsync_ErrorStatus_ThrowsWithAmazonsMessage()
    {
        _handler.Reply(HttpStatusCode.Forbidden, """{"errors":[{"code":"Unauthorized","message":"Access to requested resource is denied."}]}""");

        var error = await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateGateway().ListTransactionsPageAsync(UnitedStates, PostedAfter, nextToken: null, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
        Assert.Contains("finances.listTransactions", error.Message, StringComparison.Ordinal);
        Assert.Contains("Access to requested resource is denied.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulatedGateway_PagesThroughHeldOrdersOnly()
    {
        var now = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var gateway = new SimulatedFinancesGateway(new Microsoft.Extensions.Time.Testing.FakeTimeProvider(now));

        var items = new List<Application.Ingestion.DeferredTransactionItem>();
        string? token = null;
        do
        {
            var page = await gateway.ListTransactionsPageAsync(UnitedStates, now.AddDays(-179), token, CancellationToken.None);
            items.AddRange(page.Items);
            token = page.NextToken;
        }
        while (token is not null);

        Assert.True(items.Count > SimulatedFinancesGateway.PageSize);
        Assert.All(items, i => Assert.True(i.MaturityDate > now));
        Assert.All(items, i => Assert.StartsWith("111-", i.AmazonOrderId, StringComparison.Ordinal));
    }
}
