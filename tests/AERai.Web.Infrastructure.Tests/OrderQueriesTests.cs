using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Orders;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for the Orders page queries: search, filters, sorts, totals, items, payouts, and the export.</summary>
public sealed class OrderQueriesTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private const string Us = MarketplaceIds.UnitedStates;
    private const string Ca = MarketplaceIds.Canada;
    private static readonly DateTimeOffset Day = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Three US orders and one CA order whose ids and SKUs share a prefix unique to the test, plus a US
    /// settlement (USD) for the first US order and a CAD settlement, listed under US, for the CA order.
    /// </summary>
    private async Task SeedAsync(string p)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.AddRange(
            new Product { Sku = p + "-MAT", Asin = p + "ASIN1", Title = "Yoga mat blue", CreatedAt = Day, UpdatedAt = Day },
            new Product { Sku = p + "-STRAP", Title = "Strap black", CreatedAt = Day, UpdatedAt = Day });
        db.ProductCosts.Add(new ProductCost { Sku = p + "-MAT", MarketplaceId = Us, CostOfGoods = 6.5m, UpdatedAt = Day });
        db.Orders.AddRange(
            NewOrder(p + "-1", Us, "USD", "Shipped", Day, (p + "-MAT", 2, 40m), (p + "-STRAP", 1, 10m)),
            NewOrder(p + "-2", Us, "USD", "Pending", Day.AddDays(1), (p + "-STRAP", 3, 30m)),
            NewOrder(p + "-3", Us, "USD", "Cancelled", Day.AddDays(2), (p + "-MAT", 1, 20m)),
            NewOrder(p + "-4", Ca, "CAD", "Shipped", Day, (p + "-MAT", 1, 25m)));
        db.Settlements.AddRange(
            new Settlement
            {
                SettlementId = p + "-S-USD", MarketplaceId = Us, Currency = "USD", PeriodEnd = Day.AddDays(7),
                Lines =
                [
                    Line(p + "-S-USD", p + "-1", "Order", "ItemPrice", "Principal", 50m),
                    Line(p + "-S-USD", p + "-1", "Order", "ItemFees", "FBAPerUnitFulfillmentFee", -12m),
                ],
            },
            new Settlement
            {
                SettlementId = p + "-S-CAD", MarketplaceId = Us, Currency = "CAD", PeriodEnd = Day.AddDays(7),
                Lines = [Line(p + "-S-CAD", p + "-4", "Order", "ItemPrice", "Principal", 25m), Line(p + "-S-CAD", p + "-4", "Refund", "ItemPrice", "Principal", -25m)],
            });
        await db.SaveChangesAsync();
    }

    private static Order NewOrder(string id, string marketplaceId, string currency, string status, DateTimeOffset purchased, params (string Sku, int Quantity, decimal Price)[] items) => new()
    {
        AmazonOrderId = id, MarketplaceId = marketplaceId, Currency = currency, OrderStatus = status, PurchaseDate = purchased, UpdatedAt = purchased,
        Items = items.Select(i => new OrderItem { Sku = i.Sku, Quantity = i.Quantity, ItemPrice = i.Price }).ToList(),
    };

    private static SettlementLine Line(string settlementId, string orderId, string transactionType, string amountType, string description, decimal amount) => new()
    {
        SettlementId = settlementId, AmazonOrderId = orderId, TransactionType = transactionType, AmountType = amountType,
        AmountDescription = description, Amount = amount, PostedDate = Day.AddDays(1),
    };

    private static IOrderQueries Queries(AsyncServiceScope scope) => scope.ServiceProvider.GetRequiredService<IOrderQueries>();

    [SqlFact]
    public async Task GetOrdersAsync_SearchWords_MatchOrderIdOrAnyItemsSkuAsinOrTitle()
    {
        await SeedAsync("OQS");
        await using var scope = fixture.Services.CreateAsyncScope();

        var byTitle = await Queries(scope).GetOrdersAsync(Us, new PageRequest(Search: "OQS yoga blue"), OrderListFilter.Default, CancellationToken.None);
        var byAsin = await Queries(scope).GetOrdersAsync(Us, new PageRequest(Search: "OQSASIN1"), OrderListFilter.Default, CancellationToken.None);
        var byId = await Queries(scope).GetOrdersAsync(Us, new PageRequest(Search: "OQS-2"), OrderListFilter.Default, CancellationToken.None);

        Assert.Equal(["OQS-3", "OQS-1"], byTitle.Items.Select(o => o.AmazonOrderId));
        Assert.Equal(["OQS-3", "OQS-1"], byAsin.Items.Select(o => o.AmazonOrderId));
        Assert.Equal(["OQS-2"], byId.Items.Select(o => o.AmazonOrderId));
    }

    [SqlFact]
    public async Task GetOrdersAsync_StatusDatesAndSort_AreApplied()
    {
        await SeedAsync("OQF");
        await using var scope = fixture.Services.CreateAsyncScope();
        var search = new PageRequest(Search: "OQF");

        var pending = await Queries(scope).GetOrdersAsync(Us, search, new OrderListFilter(Status: "Pending"), CancellationToken.None);
        var firstDay = await Queries(scope).GetOrdersAsync(Us, search, new OrderListFilter(Day.AddHours(-12), Day.AddHours(12)), CancellationToken.None);
        var byUnits = await Queries(scope).GetOrdersAsync(Us, search, new OrderListFilter(Sort: OrderSort.Units), CancellationToken.None);
        var bySalesAscending = await Queries(scope).GetOrdersAsync(Us, search, new OrderListFilter(Sort: OrderSort.Sales, Descending: true), CancellationToken.None);

        Assert.Equal(["OQF-2"], pending.Items.Select(o => o.AmazonOrderId));
        Assert.Equal(["OQF-1"], firstDay.Items.Select(o => o.AmazonOrderId));
        // Orders 1 and 2 both have 3 units, so the newer comes first.
        Assert.Equal(["OQF-2", "OQF-1", "OQF-3"], byUnits.Items.Select(o => o.AmazonOrderId));
        Assert.Equal(["OQF-3", "OQF-2", "OQF-1"], bySalesAscending.Items.Select(o => o.AmazonOrderId));
    }

    [SqlFact]
    public async Task GetTotalsAsync_LeavesCancelledOrdersOutOfUnitsAndSales()
    {
        await SeedAsync("OQT");
        await using var scope = fixture.Services.CreateAsyncScope();

        var totals = await Queries(scope).GetTotalsAsync(Us, new PageRequest(Search: "OQT"), OrderListFilter.Default, CancellationToken.None);

        Assert.Equal(new OrderListTotals(Orders: 3, Cancelled: 1, Units: 6, Sales: 80m), totals);
    }

    [SqlFact]
    public async Task GetItemsAsync_ReturnsCatalogDetailsAndTheMarketplacesCost()
    {
        await SeedAsync("OQI");
        await using var scope = fixture.Services.CreateAsyncScope();

        var us = await Queries(scope).GetItemsAsync(Us, ["OQI-1"], CancellationToken.None);
        var ca = await Queries(scope).GetItemsAsync(Ca, ["OQI-4", "OQI-1"], CancellationToken.None);

        Assert.Equal([("OQI-MAT", "OQIASIN1", 6.5m), ("OQI-STRAP", null, null)], us.Select(i => (i.Sku, i.Asin, i.UnitCost)).ToList<(string, string?, decimal?)>());
        // The mat has a US cost only, and the US order isn't Canada's.
        var item = Assert.Single(ca);
        Assert.Equal(("OQI-4", null), (item.AmazonOrderId, item.UnitCost));
    }

    [SqlFact]
    public async Task GetSettlementAmountsAsync_ReadsTheSettlementListWithTheBucketRule()
    {
        await SeedAsync("OQP");
        await using var scope = fixture.Services.CreateAsyncScope();

        var amounts = await Queries(scope).GetSettlementAmountsAsync(Us, ["OQP-1", "OQP-4", "OQP-2"], CancellationToken.None);

        var usd = Assert.Single(amounts, a => a.AmazonOrderId == "OQP-1");
        Assert.Equal(("OQP-S-USD", "USD", 50m, -12m, 38m), (usd.SettlementId, usd.Currency, usd.Sales, usd.Expenses, usd.Net));
        var cad = Assert.Single(amounts, a => a.AmazonOrderId == "OQP-4");
        Assert.Equal(("OQP-S-CAD", "CAD", 25m, -25m, 0m), (cad.SettlementId, cad.Currency, cad.Sales, cad.Refunds, cad.Net));
        Assert.DoesNotContain(amounts, a => a.AmazonOrderId == "OQP-2");
    }

    [SqlFact]
    public async Task GetExportRowsAsync_OneRowPerItemInListOrder_UpToTheLimit()
    {
        await SeedAsync("OQE");
        await using var scope = fixture.Services.CreateAsyncScope();

        var all = await Queries(scope).GetExportRowsAsync(Us, new PageRequest(Search: "OQE"), OrderListFilter.Default, 100, CancellationToken.None);
        var two = await Queries(scope).GetExportRowsAsync(Us, new PageRequest(Search: "OQE"), OrderListFilter.Default, 2, CancellationToken.None);

        Assert.Equal(
            [("OQE-3", "OQE-MAT"), ("OQE-2", "OQE-STRAP"), ("OQE-1", "OQE-MAT"), ("OQE-1", "OQE-STRAP")],
            all.Select(r => (r.AmazonOrderId, r.Sku)));
        Assert.Equal(2, two.Count);
        Assert.Equal(6.5m, all[0].UnitCost);
    }
}
