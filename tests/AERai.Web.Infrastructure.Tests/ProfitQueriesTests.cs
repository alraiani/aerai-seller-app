using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Profit;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>
/// SQL-backed tests for <c>rpt.vw_ProfitLine</c> (ledger cutover, currency, local days), the cost-history lookup,
/// refunds with their order items, ledger upserts, and settlement ties.
/// </summary>
public sealed class ProfitQueriesTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>, IAsyncLifetime
{
    private const string Us = MarketplaceIds.UnitedStates;
    private const string Ca = MarketplaceIds.Canada;
    private const string NewYork = "America/New_York";
    private static readonly DateOnly Sep1 = new(2026, 9, 1);
    private static readonly DateOnly Sep30 = new(2026, 9, 30);

    private static DateTimeOffset Utc(int month, int day, int hour = 0) => new(2026, month, day, hour, 0, 0, TimeSpan.Zero);

    private static AmazonFinancialTransaction Ledger(string id, DateTimeOffset posted, string? orderId, string? settlementId, string type, params AmazonFinancialLine[] lines) =>
        new(id, type, "RELEASED", type, posted, orderId, settlementId, lines.Sum(l => l.Amount), "USD", lines);

    public async Task InitializeAsync()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // xUnit makes a new instance per test while the class shares one database: seed it once.
        if (db.Products.Any(p => p.Sku == "PQ-1"))
        {
            return;
        }

        db.Products.Add(new Product { Sku = "PQ-1", Title = "Mat", CreatedAt = Utc(1, 1), UpdatedAt = Utc(1, 1) });
        db.ProductCostHistory.AddRange(
            new ProductCostHistory { Sku = "PQ-1", MarketplaceId = Us, CostOfGoods = 4m, EffectiveFrom = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), ChangedBy = "system" },
            new ProductCostHistory { Sku = "PQ-1", MarketplaceId = Us, CostOfGoods = 5m, EffectiveFrom = Utc(9, 15), ChangedBy = "op" });
        db.Orders.Add(new Order
        {
            AmazonOrderId = "PQ-O-2", MarketplaceId = Us, PurchaseDate = Utc(9, 18), OrderStatus = "Shipped", Currency = "USD", UpdatedAt = Utc(9, 18),
            Items = [new OrderItem { Sku = "PQ-1", Quantity = 2, ItemPrice = 40m }],
        });

        // Before the ledger: settlement lines in USD (US), CAD (Canada's, in the US list), and MXN (no marketplace).
        Line Sale(string settlement, DateTimeOffset posted, decimal amount, int quantity) =>
            new(settlement, posted, "Order", "ItemPrice", "Principal", amount, quantity);
        db.Settlements.AddRange(
            NewSettlement("PQ-S-USD", "USD", Utc(8, 28), Utc(9, 11), Sale("PQ-S-USD", Utc(9, 2), 40m, 2), Sale("PQ-S-USD", Utc(9, 12), 20m, 1), Sale("PQ-S-USD", Utc(9, 14), 99m, 9)),
            NewSettlement("PQ-S-CAD", "CAD", Utc(8, 28), Utc(9, 11), Sale("PQ-S-CAD", Utc(9, 3), 30m, 1)),
            NewSettlement("PQ-S-MXN", "MXN", Utc(8, 28), Utc(9, 11), Sale("PQ-S-MXN", Utc(9, 3), 500m, 1)),
            NewSettlement("PQ-S-NEW", "USD", Utc(9, 13), Utc(9, 27), Sale("PQ-S-NEW", Utc(9, 20), 40m, 2), new Line("PQ-S-NEW", Utc(9, 20), "Order", "ItemFees", "Commission", -6m, null)));
        await db.SaveChangesAsync();

        // The ledger starts on Sep 12 (a partial day), so it counts from Sep 13.
        var ledger = scope.ServiceProvider.GetRequiredService<IFinancialTransactionRepository>();
        await ledger.UpsertAsync(Us,
        [
            Ledger("PQ-L-0", Utc(9, 12, 15), null, null, "Shipment", new AmazonFinancialLine("PQ-1", null, 1, "ProductCharges/Principal", 77m)),
            Ledger("PQ-L-1", Utc(9, 20, 2), "PQ-O-2", "PQ-S-NEW", "Shipment",
                new AmazonFinancialLine("PQ-1", null, 2, "ProductCharges/Principal", 40m),
                new AmazonFinancialLine("PQ-1", null, 0, "AmazonFees/Commission", -6m)),
            Ledger("PQ-L-2", Utc(9, 25, 16), "PQ-O-2", null, "Refund",
                new AmazonFinancialLine("PQ-1", null, 0, "ProductCharges/Principal", -20m)),
            Ledger("PQ-L-3", Utc(9, 26, 16), null, null, "ServiceFee", new AmazonFinancialLine(null, null, 0, "ServiceFees/Mystery", -1.5m)),
        ], Utc(9, 30), CancellationToken.None);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record Line(string SettlementId, DateTimeOffset Posted, string Type, string AmountType, string Description, decimal Amount, int? Quantity);

    private static Settlement NewSettlement(string id, string currency, DateTimeOffset start, DateTimeOffset end, params Line[] lines) => new()
    {
        SettlementId = id, MarketplaceId = Us, Currency = currency, PeriodStart = start, PeriodEnd = end,
        Lines = [.. lines.Select(l => new SettlementLine
        {
            SettlementId = id, PostedDate = l.Posted, TransactionType = l.Type, AmazonOrderId = l.Type == "Order" && l.Amount == 40m && id == "PQ-S-NEW" ? "PQ-O-2" : null,
            OrderItemCode = "1", Quantity = l.Quantity, Sku = "PQ-1", AmountType = l.AmountType, AmountDescription = l.Description, Amount = l.Amount,
        })],
    };

    private async Task<T> QueryAsync<T>(Func<IProfitQueries, Task<T>> query)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IProfitQueries>());
    }

    [SqlFact]
    public async Task GetAmountsAsync_UsesSettlementLinesBeforeTheLedgerAndTheLedgerAfter_ByLocalDay()
    {
        var rows = await QueryAsync(q => q.GetAmountsAsync(Us, Sep1, Sep30, NewYork, CancellationToken.None));

        Assert.Equal(
            [
                (new DateOnly(2026, 9, 2), ProfitCategory.Sales, 40m),
                (new DateOnly(2026, 9, 12), ProfitCategory.Sales, 20m),        // settlement line, before the cutover
                (new DateOnly(2026, 9, 19), ProfitCategory.Commission, -6m),   // 02:00 UTC on the 20th is the 19th in New York
                (new DateOnly(2026, 9, 19), ProfitCategory.Sales, 40m),
                (new DateOnly(2026, 9, 25), ProfitCategory.RefundedSales, -20m),
                (new DateOnly(2026, 9, 26), ProfitCategory.OtherExpenses, -1.5m),
            ],
            rows.OrderBy(r => r.Day).ThenBy(r => r.Category.ToString(), StringComparer.Ordinal).Select(r => (r.Day, r.Category, r.Amount)));
    }

    [SqlFact]
    public async Task GetAmountsAsync_Canada_GetsTheCadLinesOfTheUsListAndMexicoNothing()
    {
        var rows = await QueryAsync(q => q.GetAmountsAsync(Ca, Sep1, Sep30, "America/Toronto", CancellationToken.None));

        Assert.Equal(30m, Assert.Single(rows).Amount);
    }

    [SqlFact]
    public async Task GetSoldUnitsAndRefundsAsync_ChargeTheCostOnTheOrdersPurchaseDate()
    {
        var sold = await QueryAsync(q => q.GetSoldUnitsAsync(Us, Sep1, Sep30, NewYork, CancellationToken.None));
        var refunds = await QueryAsync(q => q.GetRefundsAsync(Us, Sep1, Sep30, NewYork, CancellationToken.None));

        // No order: the posted date's cost (4); the ledger sale's order was bought after the change (5).
        Assert.Equal(
            [(new DateOnly(2026, 9, 2), (decimal?)4m, 2), (new DateOnly(2026, 9, 12), 4m, 1), (new DateOnly(2026, 9, 19), 5m, 2)],
            sold.OrderBy(s => s.Day).Select(s => (s.Day, s.UnitCost, s.Units)));
        var refund = Assert.Single(refunds);
        Assert.Equal((-20m, (decimal?)40m, (int?)2, (decimal?)5m), (refund.RefundedPrincipal, refund.ItemPrice, refund.ItemQuantity, refund.UnitCost));
    }

    [SqlFact]
    public async Task GetSettlementTiesAsync_AddsUpTheLedgerPerSettlement_AndOtherKindsListUnmappedFees()
    {
        var ties = await QueryAsync(q => q.GetSettlementTiesAsync(Us, Utc(9, 1), Utc(10, 1), CancellationToken.None));
        var start = await QueryAsync(q => q.GetLedgerStartAsync(Us, CancellationToken.None));
        var kinds = await QueryAsync(q => q.GetOtherExpenseKindsAsync(Us, Sep1, Sep30, NewYork, 10, CancellationToken.None));

        Assert.Equal(Utc(9, 13), start);
        Assert.Equal(["PQ-S-NEW", "PQ-S-USD"], ties.Select(t => t.SettlementId));
        var tied = ties[0];
        Assert.Equal((34m, (decimal?)34m, 1), (tied.Net, tied.LedgerTotal, tied.LedgerTransactions));
        Assert.Null(ties[1].LedgerTotal);
        Assert.Equal(("ServiceFee", "ServiceFees/Mystery", -1.5m), (kinds.Single().TransactionType, kinds.Single().Detail, kinds.Single().Amount));
    }

    [SqlFact]
    public async Task UpsertAsync_ReadAgain_ReplacesLinesAndMarkReleasedClearsWhatIsNoLongerHeld()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<IFinancialTransactionRepository>();
        var held = Ledger("PQ-H-1", Utc(9, 28), null, null, "Shipment", new AmazonFinancialLine("PQ-1", null, 1, "ProductCharges/Principal", 10m)) with { Status = "DEFERRED" };

        Assert.Equal((1, 0), await ledger.UpsertAsync(Ca, [held], Utc(9, 30), CancellationToken.None));
        Assert.Equal((0, 1), await ledger.UpsertAsync(Ca, [held with { Amount = 12m, Lines = [new AmazonFinancialLine("PQ-1", null, 1, "ProductCharges/Principal", 12m)] }], Utc(9, 30), CancellationToken.None));
        Assert.Equal(0, await ledger.MarkReleasedAsync(Ca, ["PQ-H-1"], CancellationToken.None));
        Assert.Equal(1, await ledger.MarkReleasedAsync(Ca, [], CancellationToken.None));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = db.FinancialTransactions.Where(t => t.TransactionId == "PQ-H-1").Select(t => new { t.Status, t.Amount, Lines = t.Lines.Count }).Single();
        Assert.Equal(("DEFERRED_RELEASED", 12m, 1), (stored.Status, stored.Amount, stored.Lines));
    }
}
