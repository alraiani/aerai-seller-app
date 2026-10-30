using System.Net;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.SpApi;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Infrastructure.Tests.SpApi;

public sealed class InboundGatewayTests : IDisposable
{
    private const string Plans = """
        {"inboundPlans":[
          {"inboundPlanId":"wf123","name":"Hankies Oct","status":"ACTIVE","createdAt":"2026-10-20T14:00:00Z","lastUpdatedAt":"2026-10-21T09:30:00Z","marketplaceIds":["ATVPDKIKX0DER"],"sourceAddress":{}},
          {"name":"No id","status":"ACTIVE"}
        ],"pagination":{"nextToken":"tok/+="}}
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

    private SpApiInboundGateway CreateGateway() =>
        new(new InboundApiClient(new HttpClient(_handler), Options.Create(new SpApiOptions())));

    private static string? Option(HttpRequestMessage request) =>
        request.Options.TryGetValue(SpApiOperation.OptionKey, out var operation) ? operation : null;

    [Fact]
    public async Task ListPlansPageAsync_SortsNewestFirstTagsTheOperationAndDropsPlansWithoutAnId()
    {
        _handler.Reply(HttpStatusCode.OK, Plans);

        var page = await CreateGateway().ListPlansPageAsync(UnitedStates, nextToken: null, CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.Equal("https://sellingpartnerapi-na.amazon.com/inbound/fba/2024-03-20/inboundPlans?pageSize=30&sortBy=LAST_UPDATED_TIME&sortOrder=DESC", request.RequestUri!.ToString());
        Assert.Equal(SpApiOperation.ListInboundPlans, Option(request));
        Assert.True(request.Options.TryGetValue(SpApiOperation.RegionKey, out var region));
        Assert.Equal(AmazonRegion.NorthAmerica, region);

        var plan = Assert.Single(page.Plans);
        Assert.Equal(("wf123", "Hankies Oct", "ACTIVE"), (plan.InboundPlanId, plan.Name, plan.Status));
        Assert.Equal(new DateTimeOffset(2026, 10, 21, 9, 30, 0, TimeSpan.Zero), plan.LastUpdatedAt);
        Assert.Equal([MarketplaceIds.UnitedStates], plan.MarketplaceIds);
        Assert.Equal("tok/+=", page.NextToken);
        Assert.Equal(Plans, page.RawJson);
    }

    [Fact]
    public async Task ListPlansPageAsync_NextPageInEurope_EscapesTokenAndUsesEuropeEndpoint()
    {
        _handler.Reply(HttpStatusCode.OK, """{"inboundPlans":[]}""");

        var page = await CreateGateway().ListPlansPageAsync(UnitedKingdom, "tok/+=", CancellationToken.None);

        var request = Assert.Single(_handler.Requests);
        Assert.StartsWith("https://sellingpartnerapi-eu.amazon.com/inbound/fba/2024-03-20/inboundPlans?", request.RequestUri!.ToString(), StringComparison.Ordinal);
        Assert.EndsWith("&paginationToken=tok%2F%2B%3D", request.RequestUri.AbsoluteUri, StringComparison.Ordinal);
        Assert.Empty(page.Plans);
        Assert.Null(page.NextToken);
    }

    [Fact]
    public async Task PlanShipmentAndItems_ReadTheShipmentIdsTheFbaIdAndEachSku()
    {
        _handler
            .Reply(HttpStatusCode.OK, """{"inboundPlanId":"wf123","shipments":[{"shipmentId":"sh1","status":"WORKING"},{"shipmentId":"sh1","status":"WORKING"},{"shipmentId":"sh2","status":"UNCONFIRMED"}]}""")
            .Reply(HttpStatusCode.OK, """{"shipmentId":"sh1","shipmentConfirmationId":" FBA15ABCD ","name":"Shipment 1","status":"READY_TO_SHIP","placementOptionId":"po1","destination":{},"source":{}}""")
            .Reply(HttpStatusCode.OK, """{"items":[{"msku":"HK-BL","quantity":25,"asin":"B0","fnsku":"X0","labelOwner":"SELLER","prepInstructions":[]},{"msku":" ","quantity":3},{"msku":"HK-PK","quantity":-1}],"pagination":{"nextToken":"n2"}}""");
        var gateway = CreateGateway();

        var plan = await gateway.GetPlanShipmentsAsync(UnitedStates, "wf123", CancellationToken.None);
        var shipment = await gateway.GetShipmentAsync(UnitedStates, "wf123", "sh1", CancellationToken.None);
        var items = await gateway.ListShipmentItemsPageAsync(UnitedStates, "wf123", "sh1", "n1", CancellationToken.None);

        Assert.Equal(["sh1", "sh2"], plan.ShipmentIds);
        Assert.Equal(("FBA15ABCD", "Shipment 1", "READY_TO_SHIP"), (shipment.ShipmentConfirmationId, shipment.Name, shipment.Status));
        Assert.Equal([("HK-BL", 25), ("HK-PK", 0)], items.Items.Select(i => (i.Sku, i.Quantity)));
        Assert.Equal("n2", items.NextToken);
        Assert.Equal(
            [
                "/inbound/fba/2024-03-20/inboundPlans/wf123",
                "/inbound/fba/2024-03-20/inboundPlans/wf123/shipments/sh1",
                "/inbound/fba/2024-03-20/inboundPlans/wf123/shipments/sh1/items",
            ],
            _handler.Requests.Select(r => r.RequestUri!.AbsolutePath));
        Assert.Equal("?pageSize=1000&paginationToken=n1", _handler.Requests[2].RequestUri!.Query);
        Assert.Equal(
            [SpApiOperation.GetInboundPlan, SpApiOperation.GetInboundShipment, SpApiOperation.ListInboundShipmentItems],
            _handler.Requests.Select(Option));
    }

    [Fact]
    public async Task GetShipmentAsync_NotConfirmedYet_HasNoFbaId()
    {
        _handler.Reply(HttpStatusCode.OK, """{"shipmentId":"sh2","shipmentConfirmationId":"","status":"UNCONFIRMED"}""");

        var shipment = await CreateGateway().GetShipmentAsync(UnitedStates, "wf123", "sh2", CancellationToken.None);

        Assert.Null(shipment.ShipmentConfirmationId);
    }

    [Fact]
    public async Task ListPlansPageAsync_ErrorStatus_ThrowsWithAmazonsMessage()
    {
        _handler.Reply(HttpStatusCode.Forbidden, """{"errors":[{"code":"Unauthorized","message":"Access to requested resource is denied."}]}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => CreateGateway().ListPlansPageAsync(UnitedStates, null, CancellationToken.None));

        Assert.Equal(HttpStatusCode.Forbidden, ex.StatusCode);
        Assert.Contains("fbaInbound.listInboundPlans", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Access to requested resource is denied.", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SimulatedGateway_ReturnsTheSameWeeklyShipmentsEveryTime()
    {
        var gateway = new SimulatedInboundGateway(new FakeTimeProvider(new DateTimeOffset(2026, 10, 28, 15, 0, 0, TimeSpan.Zero)));

        async Task<List<(string Fba, string Status, int Units)>> ReadAsync()
        {
            var shipments = new List<(string, string, int)>();
            string? token = null;
            do
            {
                var page = await gateway.ListPlansPageAsync(UnitedStates, token, CancellationToken.None);
                foreach (var plan in page.Plans)
                {
                    foreach (var id in (await gateway.GetPlanShipmentsAsync(UnitedStates, plan.InboundPlanId, CancellationToken.None)).ShipmentIds)
                    {
                        var shipment = await gateway.GetShipmentAsync(UnitedStates, plan.InboundPlanId, id, CancellationToken.None);
                        var items = await gateway.ListShipmentItemsPageAsync(UnitedStates, plan.InboundPlanId, id, null, CancellationToken.None);
                        shipments.Add((shipment.ShipmentConfirmationId!, shipment.Status, items.Items.Sum(i => i.Quantity))); // Non-null: simulated shipments are all confirmed.
                    }
                }

                token = page.NextToken;
            }
            while (token is not null);

            return shipments;
        }

        var first = await ReadAsync();
        var second = await ReadAsync();

        Assert.Equal(SimulatedInboundGateway.Weeks, first.Count);
        Assert.Equal(first, second);
        Assert.Equal("WORKING", first[0].Status);
        Assert.All(first, s => Assert.StartsWith("FBAUSSIM", s.Fba, StringComparison.Ordinal));
        Assert.All(first, s => Assert.True(s.Units > 0 && s.Units % 5 == 0));
    }
}
