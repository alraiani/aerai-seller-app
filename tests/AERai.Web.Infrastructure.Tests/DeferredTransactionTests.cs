using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Settlements;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for <c>core.DeferredTransaction</c>: applying Amazon's listing and the reads behind the pages.</summary>
/// <remarks>Each test uses its own transaction and order ids; the held totals test uses the United Kingdom so no other test adds to them.</remarks>
public sealed class DeferredTransactionTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private static DeferredTransactionItem Item(string id, string orderId, decimal amount, DateTimeOffset maturity, string currency = "USD") =>
        new(id, orderId, "Shipment", "Order Payment", Now.AddDays(-4), amount, currency, "DD7", maturity);

    private async Task<DeferredApplyResult> ApplyAsync(string marketplaceId, DateTimeOffset seenAt, params DeferredTransactionItem[] items)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IDeferredTransactionRepository>().ApplyAsync(marketplaceId, items, seenAt, CancellationToken.None);
    }

    private async Task<T> QueryAsync<T>(Func<IDeferredPaymentQueries, Task<T>> query)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<IDeferredPaymentQueries>());
    }

    [SqlFact]
    public async Task ApplyAsync_TransactionGoneFromTheListing_IsReleasedAtItsMaturityOnceThatHasPassed()
    {
        var first = await ApplyAsync(MarketplaceIds.UnitedStates, Now,
            Item("T-DT-1", "113-DT-1", 10m, Now.AddDays(-1)),
            Item("T-DT-2", "113-DT-2", 20m, Now.AddDays(3)),
            Item("T-DT-3", "113-DT-3", 30m, Now.AddDays(5)));
        var later = Now.AddDays(1);
        var second = await ApplyAsync(MarketplaceIds.UnitedStates, later, Item("T-DT-3", "113-DT-3", 31m, Now.AddDays(5)));

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
    public async Task GetHeldAsync_SumsHeldTransactionsPerCurrencyAndCountsOrdersOnce()
    {
        await ApplyAsync(MarketplaceIds.UnitedKingdom, Now,
            Item("T-UK-1", "203-UK-1", 10m, Now.AddDays(4), "GBP"),
            Item("T-UK-2", "203-UK-1", 2.5m, Now.AddDays(2), "GBP"),
            Item("T-UK-3", "203-UK-2", 7m, Now.AddDays(6), "GBP"));

        var held = Assert.Single(await QueryAsync(q => q.GetHeldAsync(MarketplaceIds.UnitedKingdom, CancellationToken.None)));
        var page = await QueryAsync(q => q.GetPageAsync(MarketplaceIds.UnitedKingdom, new PageRequest(Search: "203-UK-2"), new DeferredListFilter(), Now.AddDays(-30), CancellationToken.None));

        Assert.Equal(new DeferredHeld("GBP", 19.5m, 3, 2, Now.AddDays(2)), held);
        Assert.Equal("T-UK-3", Assert.Single(page.Items).TransactionId);
    }

    [SqlFact]
    public async Task GetReleasedInSettlementAsync_FindsReleasedDeferralsOfTheSettlementsOrders()
    {
        await ApplyAsync(MarketplaceIds.Canada, Now, Item("T-REL-CA", "701-REL-1", 15m, Now.AddDays(-2), "CAD"), Item("T-HELD-CA", "701-REL-2", 4m, Now.AddDays(4), "CAD"));
        await ApplyAsync(MarketplaceIds.Canada, Now.AddDays(1), Item("T-HELD-CA", "701-REL-2", 4m, Now.AddDays(4), "CAD"));
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
