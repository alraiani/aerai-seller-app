using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for <c>core.DeferredTransaction</c>: applying Amazon's listings and the reads behind the pages.</summary>
/// <remarks>
/// Each test uses its own transaction, order, and settlement ids; the held totals and groups tests use the United
/// Kingdom so no other test adds to them.
/// </remarks>
public sealed class DeferredTransactionTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static AmazonDeferredTransaction Item(
        string id, string orderId, decimal amount, DateTimeOffset maturity, string currency = "USD", string? settlementId = null, params AmazonDeferredProduct[] products) =>
        new(id, orderId, settlementId, "Shipment", "Order Payment", Now.AddDays(-4), amount, currency, "DD7", maturity, products);

    private async Task<DeferredApplyResult> ApplyAsync(string marketplaceId, DateTimeOffset seenAt, AmazonDeferredTransaction[] held, params AmazonDeferredTransaction[] released)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDeferredTransactionRepository>().ApplyAsync(marketplaceId, held, released, seenAt, CancellationToken.None);
    }

    private async Task<T> QueryAsync<T>(Func<IDeferredPaymentQueries, Task<T>> query)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IDeferredPaymentQueries>());
    }

    private async Task AddSettlementAsync(string settlementId, string marketplaceId, string currency, DateTimeOffset periodEnd)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Settlements.Add(new Settlement { SettlementId = settlementId, MarketplaceId = marketplaceId, Currency = currency, PeriodStart = periodEnd.AddDays(-14), PeriodEnd = periodEnd });
        await db.SaveChangesAsync();
    }

    [SqlFact]
    public async Task ApplyAsync_TransactionGoneFromBothListings_IsReleasedAtItsMaturityOnceThatHasPassed()
    {
        var first = await ApplyAsync(MarketplaceIds.UnitedStates, Now,
        [
            Item("T-DT-1", "113-DT-1", 10m, Now.AddDays(-1)),
            Item("T-DT-2", "113-DT-2", 20m, Now.AddDays(3)),
            Item("T-DT-3", "113-DT-3", 30m, Now.AddDays(5)),
        ]);
        var later = Now.AddDays(1);
        var second = await ApplyAsync(MarketplaceIds.UnitedStates, later, [Item("T-DT-3", "113-DT-3", 31m, Now.AddDays(5))]);

        Assert.Equal((3, 0), (first.Deferred, first.NewlyReleased));
        Assert.Equal((1, 2), (second.Deferred, second.NewlyReleased));

        await using var scope = fixture.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<AppDbContext>().DeferredTransactions.AsNoTracking()
            .Where(t => t.TransactionId.StartsWith("T-DT-"))
            .ToDictionaryAsync(t => t.TransactionId);

        // Past its maturity: released then. Not yet due but gone: released when the sync noticed.
        Assert.Equal((DeferredTransactionStatus.Released, Now.AddDays(-1)), (rows["T-DT-1"].Status, rows["T-DT-1"].ReleasedAt));
        Assert.Equal((DeferredTransactionStatus.Released, later), (rows["T-DT-2"].Status, rows["T-DT-2"].ReleasedAt));
        Assert.Equal((DeferredTransactionStatus.Deferred, 31m, Now, later), (rows["T-DT-3"].Status, rows["T-DT-3"].Amount, rows["T-DT-3"].FirstSeenAt, rows["T-DT-3"].LastSeenAt));
    }

    [SqlFact]
    public async Task ApplyAsync_ReleasedListing_AddsHistoryWithProductsAndLeavesKnownReleasesAlone()
    {
        var product = new AmazonDeferredProduct("SKU-RL", "B0RL", 2, 40m, 25m);
        var held = Item("T-RL-HELD", "113-RL-1", 25m, Now.AddDays(-2), settlementId: "S-RL", products: product);
        await ApplyAsync(MarketplaceIds.UnitedStates, Now.AddDays(-5), [held]);

        // Now Amazon lists it as released, along with an older release never seen held.
        var result = await ApplyAsync(MarketplaceIds.UnitedStates, Now, [],
            held, Item("T-RL-OLD", "113-RL-2", 9m, Now.AddDays(-60), settlementId: "S-RL-OLD", products: new AmazonDeferredProduct("SKU-RL", "B0RL", 1, 20m, 9m)));
        var again = await ApplyAsync(MarketplaceIds.UnitedStates, Now.AddDays(1), [], Item("T-RL-OLD", "113-RL-2", 99m, Now.AddDays(-60), settlementId: "S-RL-OLD"));

        Assert.Equal((0, 2, 1), (result.Deferred, result.Released, result.NewlyReleased));
        Assert.Equal(0, again.NewlyReleased);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.DeferredTransactions.AsNoTracking().Include(t => t.Items).Where(t => t.TransactionId.StartsWith("T-RL-")).ToDictionaryAsync(t => t.TransactionId);
        Assert.Equal((DeferredTransactionStatus.Released, "S-RL", Now.AddDays(-2)), (rows["T-RL-HELD"].Status, rows["T-RL-HELD"].SettlementId, rows["T-RL-HELD"].ReleasedAt));
        Assert.Equal(("SKU-RL", 2, 40m, 25m), (rows["T-RL-HELD"].Items.Single().Sku, rows["T-RL-HELD"].Items.Single().Quantity, rows["T-RL-HELD"].Items.Single().Principal, rows["T-RL-HELD"].Items.Single().Amount));

        // A release Amazon listed before is history: the later listing does not rewrite it.
        Assert.Equal((9m, 1), (rows["T-RL-OLD"].Amount, rows["T-RL-OLD"].Items.Count));
        Assert.True(await scope.ServiceProvider.GetRequiredService<IDeferredTransactionRepository>().HasReleaseHistoryAsync(MarketplaceIds.UnitedStates, CancellationToken.None));
    }

    [SqlFact]
    public async Task GetHeldAsync_SumsHeldTransactionsPerCurrencyAndCountsOrdersOnce()
    {
        await ApplyAsync(MarketplaceIds.UnitedKingdom, Now,
        [
            Item("T-UK-1", "203-UK-1", 10m, Now.AddDays(4), "GBP"),
            Item("T-UK-2", "203-UK-1", 2.5m, Now.AddDays(2), "GBP"),
            Item("T-UK-3", "203-UK-2", 7m, Now.AddDays(6), "GBP"),
        ]);

        var held = Assert.Single(await QueryAsync(q => q.GetHeldAsync(MarketplaceIds.UnitedKingdom, CancellationToken.None)));
        var page = await QueryAsync(q => q.GetPageAsync(MarketplaceIds.UnitedKingdom, new PageRequest(Search: "203-UK-2"), new DeferredListFilter(), CancellationToken.None));

        Assert.Equal(new DeferredHeld("GBP", 19.5m, 3, 2, Now.AddDays(2)), held);
        Assert.Equal("T-UK-3", Assert.Single(page.Items).TransactionId);
    }

    [SqlFact]
    public async Task SettlementReads_AddUpWhatWasDeferredOutOfEachSettlement_BySkuAndPriceEach()
    {
        await AddSettlementAsync("S-OUT-1", MarketplaceIds.UnitedStates, "CAD", Now.AddDays(-10));

        // Canada's payouts are listed with the United States, so its deferrals of S-OUT-1 count there.
        await ApplyAsync(MarketplaceIds.Canada, Now,
        [
            Item("T-OUT-1", "701-OUT-1", 12m, Now.AddDays(2), "CAD", "S-OUT-1", new AmazonDeferredProduct("SKU-O", null, 1, 19.99m, 12m)),
            Item("T-OUT-2", "701-OUT-2", 24m, Now.AddDays(3), "CAD", "S-OUT-1", new AmazonDeferredProduct("SKU-O", null, 2, 39.98m, 24m)),
        ],
        Item("T-OUT-3", "701-OUT-3", 8m, Now.AddDays(-1), "CAD", "S-OUT-1", new AmazonDeferredProduct("SKU-P", null, 1, 12.5m, 8m)));

        string[] listed = [MarketplaceIds.UnitedStates, MarketplaceIds.Canada];
        var bySettlement = Assert.Single(await QueryAsync(q => q.GetBySettlementsAsync(listed, ["S-OUT-1", "S-OUT-NONE"], CancellationToken.None)));
        var bySku = await QueryAsync(q => q.GetSkuDeferralsAsync(listed, "S-OUT-1", CancellationToken.None));
        var lines = await QueryAsync(q => q.GetSkuLinesAsync(listed, "S-OUT-1", "SKU-O", 19.99m, CancellationToken.None));
        var otherList = await QueryAsync(q => q.GetBySettlementsAsync([MarketplaceIds.UnitedStates], ["S-OUT-1"], CancellationToken.None));

        Assert.Equal(new SettlementDeferral("S-OUT-1", "CAD", 44m, 3, 3, 36m, 2, Now.AddDays(-1), Now.AddDays(3)), bySettlement);

        // 1 × 19.99 and 2 × 39.98 are the same price each; the SKU rows add up to the settlement's.
        Assert.Equal(new SkuDeferral("SKU-O", 19.99m, 3, 36m, 2, "CAD"), bySku.Single(r => r.Sku == "SKU-O"));
        Assert.Equal(bySettlement.Amount, bySku.Sum(r => r.Amount));
        Assert.Equal(["701-OUT-1", "701-OUT-2"], lines.Select(l => l.AmazonOrderId));
        Assert.Empty(otherList);
    }

    [SqlFact]
    public async Task GetGroupsAsync_NewestFirst_TheOpenSettlementByItsLatestSale_WithCappedRows()
    {
        await AddSettlementAsync("S-GRP-OLD", MarketplaceIds.UnitedKingdom, "GBP", Now.AddDays(-30));
        await AddSettlementAsync("S-GRP-NEW", MarketplaceIds.UnitedKingdom, "GBP", Now.AddDays(-10));
        await ApplyAsync(MarketplaceIds.UnitedKingdom, Now,
        [
            Item("T-GRP-1", "203-GRP-1", 5m, Now.AddDays(3), "GBP", "S-GRP-OPEN"),
            Item("T-GRP-2", "203-GRP-2", 6m, Now.AddDays(1), "GBP", "S-GRP-NEW"),
            Item("T-GRP-3", "203-GRP-3", 7m, Now.AddDays(2), "GBP", "S-GRP-NEW"),
        ],
        Item("T-GRP-4", "203-GRP-4", 8m, Now.AddDays(-20), "GBP", "S-GRP-OLD"));

        var all = await QueryAsync(q => q.GetGroupsAsync(MarketplaceIds.UnitedKingdom, MarketplaceIds.UnitedKingdom, new PageRequest(Search: "GRP"), DeferredView.All, 1, CancellationToken.None));
        var held = await QueryAsync(q => q.GetGroupsAsync(MarketplaceIds.UnitedKingdom, MarketplaceIds.UnitedKingdom, new PageRequest(Search: "GRP"), DeferredView.Deferred, 10, CancellationToken.None));

        // The open settlement's sales posted after the newest imported period ended, so it comes first.
        Assert.Equal(["S-GRP-OPEN", "S-GRP-NEW", "S-GRP-OLD"], all.Items.Select(g => g.SettlementId));
        Assert.True(all.Items[0].IsOpen);
        Assert.True(all.Items[2].IsImported);
        var newest = all.Items[1];
        Assert.Equal((13m, 2, false, true), (newest.Amount, newest.Count, newest.IsOpen, newest.IsListed));

        // Rows are capped per group, soonest release first.
        Assert.Equal("T-GRP-2", Assert.Single(newest.Rows).TransactionId);
        Assert.True(newest.HasMoreRows);
        Assert.Equal(["S-GRP-OPEN", "S-GRP-NEW"], held.Items.Select(g => g.SettlementId));
    }

    [SqlFact]
    public async Task GetReleasedInSettlementAsync_FindsReleasedDeferralsOfTheSettlementsOrders()
    {
        await ApplyAsync(MarketplaceIds.Canada, Now, [Item("T-REL-CA", "701-REL-1", 15m, Now.AddDays(-2), "CAD"), Item("T-HELD-CA", "701-REL-2", 4m, Now.AddDays(4), "CAD")]);
        await ApplyAsync(MarketplaceIds.Canada, Now.AddDays(1), [Item("T-HELD-CA", "701-REL-2", 4m, Now.AddDays(4), "CAD")]);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            // Canada's payouts are listed with the United States.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Settlements.Add(new Settlement
            {
                SettlementId = "S-REL-1",
                MarketplaceId = MarketplaceIds.UnitedStates,
                Currency = "CAD",
                Lines =
                [
                    new SettlementLine { SettlementId = "S-REL-1", TransactionType = "Order", AmazonOrderId = "701-REL-1", AmountType = "ItemPrice", AmountDescription = "Principal", Amount = 20m },
                    new SettlementLine { SettlementId = "S-REL-1", TransactionType = "Order", AmazonOrderId = "701-REL-2", AmountType = "ItemPrice", AmountDescription = "Principal", Amount = 5m },
                ],
            });
            await db.SaveChangesAsync();
        }

        var released = await QueryAsync(q => q.GetReleasedInSettlementAsync(MarketplaceIds.UnitedStates, "S-REL-1", CancellationToken.None));
        var byOrder = await QueryAsync(q => q.GetByOrdersAsync([MarketplaceIds.Canada], ["701-REL-1", "701-REL-2"], CancellationToken.None));
        var otherList = await QueryAsync(q => q.GetReleasedInSettlementAsync(MarketplaceIds.Canada, "S-REL-1", CancellationToken.None));

        // Only the released one: the order still held is not paid out by this settlement.
        Assert.Equal("T-REL-CA", Assert.Single(released).TransactionId);
        Assert.Equal(2, byOrder.Count);
        Assert.Empty(otherList);
    }

    [SqlFact]
    public async Task GetSyncStateAsync_NoScheduleOrRun_IsNotSynced()
    {
        var state = await QueryAsync(q => q.GetSyncStateAsync(MarketplaceIds.UnitedKingdom, CancellationToken.None));

        Assert.Null(state.LastSuccessAt);
    }
}
