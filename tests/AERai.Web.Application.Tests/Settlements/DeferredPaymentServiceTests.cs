using AERai.Web.Application.Common;
using AERai.Web.Application.Settlements;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
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
    public async Task GetGroupsAsync_AsksForTheMarketplacesListAndCapsRows()
    {
        var canada = TestMarketplaces.Canada;
        canada.SettlementMarketplaceId = MarketplaceIds.UnitedStates;

        await CreateService().GetGroupsAsync(canada, new PageRequest(), DeferredView.All, CancellationToken.None);

        // Canada's payouts are in the United States list, so that is where its groups' Summary links lead.
        Assert.Equal((MarketplaceIds.Canada, MarketplaceIds.UnitedStates, DeferredPaymentService.MaxGroupRows), _queries.GroupsAsked);
    }

    [Fact]
    public async Task GetDeferredOutAsync_InTheMarketplacesCurrency_ConvertsAtTheSettlementsRate()
    {
        _marketplaces.Marketplaces.Single(m => m.Code == "CA").SettlementMarketplaceId = MarketplaceIds.UnitedStates;
        _queries.BySettlement.Add(new SettlementDeferral("S-CAD", "CAD", 100m, 4, 4, 40m, 2, Now.AddDays(-1), Now.AddDays(3)));
        _queries.BySettlement.Add(new SettlementDeferral("S-USD", "USD", 55.5m, 3, 3, 0m, 0, Now.AddDays(-9), Now.AddDays(-2)));
        var cad = new SettlementSummary { MarketplaceId = MarketplaceIds.UnitedStates, SettlementId = "S-CAD", Currency = "CAD", HomeCurrency = "USD", ExchangeRate = 0.7215m };
        var usd = new SettlementSummary { MarketplaceId = MarketplaceIds.UnitedStates, SettlementId = "S-USD", Currency = "USD", HomeCurrency = "USD", ExchangeRate = 1m };
        var none = new SettlementSummary { MarketplaceId = MarketplaceIds.UnitedStates, SettlementId = "S-NONE", Currency = "USD", HomeCurrency = "USD", ExchangeRate = 1m };

        var home = await CreateService().GetDeferredOutAsync(TestMarketplaces.UnitedStates, [cad, usd, none], SettlementAmounts.Home, CancellationToken.None);
        var asPaid = await CreateService().GetDeferredOutAsync(TestMarketplaces.UnitedStates, [cad], SettlementAmounts.Original, CancellationToken.None);

        Assert.Equal(new DeferredOut(72.15m, 28.86m, "USD", 4, 4, 2, Now.AddDays(-1), Now.AddDays(3)), home["S-CAD"]);
        Assert.True(home["S-USD"].IsAllReleased);
        Assert.False(home.ContainsKey("S-NONE"));
        Assert.Equal((100m, "CAD"), (asPaid["S-CAD"].Amount, asPaid["S-CAD"].Currency));

        // The United States list holds Canada's payouts, so its deferrals are read too.
        Assert.Equal([MarketplaceIds.UnitedStates, MarketplaceIds.Canada], _queries.MarketplacesAsked);
    }

    [Fact]
    public async Task GetDeferredOutAsync_NoRateYet_StaysAsPaid()
    {
        _queries.BySettlement.Add(new SettlementDeferral("S-MXN", "MXN", 500m, 2, 2, 500m, 2, null, null));
        var mxn = new SettlementSummary { MarketplaceId = MarketplaceIds.UnitedStates, SettlementId = "S-MXN", Currency = "MXN", HomeCurrency = "USD", ExchangeRate = null };

        var shown = await CreateService().GetDeferredOutAsync(TestMarketplaces.UnitedStates, [mxn], SettlementAmounts.Home, CancellationToken.None);

        Assert.Equal((500m, "MXN"), (shown["S-MXN"].Amount, shown["S-MXN"].Currency));
    }

    [Fact]
    public async Task GetSkuDeferralsAsync_ConvertsAmountsButKeepsThePriceEachAsPaid()
    {
        _queries.BySku.Add(new SkuDeferral("SKU-1", 25.99m, 3, 50m, 3, "CAD"));
        var cad = new SettlementSummary { MarketplaceId = MarketplaceIds.UnitedStates, SettlementId = "S-CAD", Currency = "CAD", HomeCurrency = "USD", ExchangeRate = 0.7m };

        var rows = await CreateService().GetSkuDeferralsAsync(TestMarketplaces.UnitedStates, cad, SettlementAmounts.Home, CancellationToken.None);

        // The price each still matches the summary rows' own (as paid) price each.
        Assert.Equal(new SkuDeferral("SKU-1", 25.99m, 3, 35m, 3, "USD"), Assert.Single(rows));
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
