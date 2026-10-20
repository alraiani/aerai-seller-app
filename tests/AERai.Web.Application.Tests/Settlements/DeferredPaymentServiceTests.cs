using AERai.Web.Application.Common;
using AERai.Web.Application.Settlements;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Settlements;

public sealed class DeferredPaymentServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeDeferredPaymentQueries _queries = new();
    private readonly FakeMarketplaceQueries _marketplaces = new();

    private DeferredPaymentService CreateService() => new(_queries, _marketplaces, new FakeTimeProvider(Now));

    private static DeferredPaymentRow Row(string id, string? orderId, DeferredTransactionStatus status, decimal amount = 10m, string currency = "USD", DateTimeOffset? maturity = null, DateTimeOffset? releasedAt = null) =>
        new(id, orderId, "Order Payment", "DD7", Now.AddDays(-5), maturity ?? Now.AddDays(2), amount, currency, status, releasedAt);

    [Fact]
    public async Task GetOverviewAsync_SeveralCurrencies_PutsTheMarketplacesOwnFirstAndNeverAddsThem()
    {
        _queries.Held.Add(new DeferredHeld("CAD", 50m, 2, 2, Now.AddDays(1)));
        _queries.Held.Add(new DeferredHeld("USD", 900m, 30, 28, Now.AddDays(3)));
        _queries.Sync = new DeferredSyncState(true, true, Now.AddHours(-2));

        var overview = await CreateService().GetOverviewAsync(TestMarketplaces.UnitedStates, CancellationToken.None);

        Assert.Equal(["USD", "CAD"], overview.Held.Select(h => h.Currency));
        Assert.Equal(30, overview.Orders);
        Assert.Equal(Now.AddDays(1), overview.NextMaturity);
        Assert.False(overview.ReleaseDue);
        Assert.True(overview.IsSynced);
    }

    [Fact]
    public async Task GetOverviewAsync_ReleaseDatePassed_IsDue()
    {
        // Amazon keeps listing a transaction as deferred until the money moves, a day or so after its release date.
        _queries.Held.Add(new DeferredHeld("USD", 10m, 1, 1, Now.AddHours(-30)));

        var overview = await CreateService().GetOverviewAsync(TestMarketplaces.UnitedStates, CancellationToken.None);

        Assert.True(overview.ReleaseDue);
        Assert.True(overview.IsDue(Now.AddHours(-30)));
        Assert.False(overview.IsDue(Now.AddDays(2)));
    }

    [Fact]
    public async Task GetOverviewAsync_NeverSynced_IsNotSynced()
    {
        _queries.Sync = new DeferredSyncState(true, false, null);

        var overview = await CreateService().GetOverviewAsync(TestMarketplaces.UnitedStates, CancellationToken.None);

        Assert.False(overview.IsSynced);
        Assert.Null(overview.NextMaturity);
    }

    [Fact]
    public async Task GetPageAsync_ReleasedView_LooksBackThirtyDays()
    {
        await CreateService().GetPageAsync(TestMarketplaces.UnitedStates, new PageRequest(), new DeferredListFilter(DeferredView.Released), CancellationToken.None);

        Assert.Equal(Now - DeferredPaymentService.ReleasedWindow, _queries.ReleasedSinceSeen);
    }

    [Fact]
    public async Task GetSettlementDeferralsAsync_CountsOrdersOnceAndSumsPerCurrency()
    {
        _queries.ReleasedInSettlement.AddRange(
        [
            Row("T-1", "111-1", DeferredTransactionStatus.Released, 10m),
            Row("T-2", "111-1", DeferredTransactionStatus.Released, 5m),
            Row("T-3", "111-2", DeferredTransactionStatus.Released, 7m),
            Row("T-4", "701-1", DeferredTransactionStatus.Released, 3m, "CAD"),
        ]);

        var deferrals = await CreateService().GetSettlementDeferralsAsync(TestMarketplaces.UnitedStates, "S1", CancellationToken.None);

        Assert.Equal(3, deferrals.Orders);
        Assert.Equal([("USD", 22m), ("CAD", 3m)], deferrals.Amounts);
    }

    [Fact]
    public async Task GetSettlementDeferralsAsync_NoneReleased_IsNone()
    {
        var deferrals = await CreateService().GetSettlementDeferralsAsync(TestMarketplaces.UnitedStates, "S1", CancellationToken.None);

        Assert.Same(SettlementDeferrals.None, deferrals);
    }

    [Fact]
    public async Task GetOrderDeferralsAsync_HeldBeatsReleased_AndListedMarketplacesAreIncluded()
    {
        _queries.Rows.Add((MarketplaceIds.UnitedStates, Row("T-1", "111-1", DeferredTransactionStatus.Released, releasedAt: Now.AddDays(-9))));
        _queries.Rows.Add((MarketplaceIds.UnitedStates, Row("T-2", "111-1", DeferredTransactionStatus.Deferred)));
        _queries.Rows.Add((MarketplaceIds.UnitedStates, Row("T-3", "111-2", DeferredTransactionStatus.Released, releasedAt: Now.AddDays(-9))));
        _queries.Rows.Add((MarketplaceIds.UnitedStates, Row("T-4", "111-2", DeferredTransactionStatus.Released, releasedAt: Now.AddDays(-2))));

        // Canada's payouts are listed with the United States, so its orders count there too.
        _marketplaces.Marketplaces.Single(m => m.Code == "CA").SettlementMarketplaceId = MarketplaceIds.UnitedStates;
        _queries.Rows.Add((MarketplaceIds.Canada, Row("T-5", "701-1", DeferredTransactionStatus.Released, currency: "CAD")));

        var byOrder = await CreateService().GetOrderDeferralsAsync(TestMarketplaces.UnitedStates, ["111-1", "111-2", "701-1", "111-9"], CancellationToken.None);

        Assert.Equal("T-2", byOrder["111-1"].TransactionId);
        Assert.Equal("T-4", byOrder["111-2"].TransactionId);
        Assert.Equal("T-5", byOrder["701-1"].TransactionId);
        Assert.False(byOrder.ContainsKey("111-9"));
    }
}
