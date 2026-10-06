using AERai.Web.Application.Alerts;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Alerts;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Alerts;

public sealed class StockAlertServiceTests
{
    // Mon 2026-10-05 15:00 UTC = 11:00 EDT.
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);
    private static readonly Marketplace Us = TestMarketplaces.UnitedStates;
    private const string Ann = "ann@aeraigroup.com";
    private const string Bob = "bob@aeraigroup.com";

    private readonly FakeInventoryQueries _inventory = new();
    private readonly FakeStockAlertRepository _repository = new();
    private readonly FakeTimeProvider _clock = new(Now);

    // Lead times total 10 days to order (5 + 0 + 5 + 0); alerts fire 7 days before an action is due.
    private readonly InventoryOptions _options = new() { SupplierLeadTimeDays = 5, PrepTimeDays = 0, TransitDays = 5, SafetyStockDays = 0, TargetStockDays = 60, AlertLeadDays = 7 };

    private StockAlertService CreateService()
    {
        var options = Options.Create(_options);
        return new StockAlertService(new InventoryService(_inventory, options, _clock), _repository, _clock, NullLogger<StockAlertService>.Instance);
    }

    /// <summary>A SKU selling 1 unit a day for the last 30 days with this much available.</summary>
    private void Selling(string sku, int available)
    {
        _inventory.Positions.RemoveAll(p => p.Sku == sku);
        _inventory.Positions.Add(new InventoryPosition { MarketplaceId = Us.MarketplaceId, Sku = sku, Available = available, SnapshotDate = new DateOnly(2026, 10, 5) });
        if (!_inventory.Sold.Any(s => s.Sku == sku))
        {
            _inventory.Sold.Add(new UnitsSold(sku, Now.AddDays(-29), 30));
        }
    }

    [Fact]
    public async Task RefreshAsync_RaisesOutAndLowAndLeavesHealthySkusAlone()
    {
        Selling("OUT", available: 0);
        Selling("LOW", available: 15);     // stockout in 15 days, order due in 5 days -> within 7
        Selling("FINE", available: 40);    // order due in 30 days
        Selling("FULL", available: 90);    // above the 60-day target: nothing to do

        await CreateService().RefreshAsync(Us, CancellationToken.None);

        Assert.Equal(
            [("LOW", StockAlertLevel.Low), ("OUT", StockAlertLevel.Out)],
            _repository.Open.OrderBy(a => a.Sku).Select(a => (a.Sku, a.Level)));
        Assert.Contains("Order 45 by Oct 10", _repository.Open.Single(a => a.Sku == "LOW").Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RefreshAsync_RunTwice_RaisesNothingNew()
    {
        Selling("OUT", available: 0);
        var service = CreateService();

        await service.RefreshAsync(Us, CancellationToken.None);
        var second = await service.RefreshAsync(Us, CancellationToken.None);

        Assert.True(second.IsEmpty);
        Assert.Single(_repository.Alerts);
    }

    [Fact]
    public async Task RefreshAsync_LowBecomesOut_ResolvesAndRaisesSoUsersAreNotifiedAgain()
    {
        Selling("SKU", available: 15);
        var service = CreateService();
        await service.RefreshAsync(Us, CancellationToken.None);
        await service.MarkAllReadAsync(Us.MarketplaceId, Ann, CancellationToken.None);

        Selling("SKU", available: 0);
        await service.RefreshAsync(Us, CancellationToken.None);

        Assert.Equal(2, _repository.Alerts.Count);
        Assert.NotNull(_repository.Alerts.Single(a => a.Level == StockAlertLevel.Low).ResolvedAt);
        Assert.Equal(1, await service.CountUnreadAsync(Us.MarketplaceId, Ann, CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAsync_Restocked_ResolvesTheAlert()
    {
        Selling("SKU", available: 0);
        var service = CreateService();
        await service.RefreshAsync(Us, CancellationToken.None);

        Selling("SKU", available: 200);
        await service.RefreshAsync(Us, CancellationToken.None);

        Assert.Empty(_repository.Open);
        var resolved = Assert.Single(await service.ListAsync(Us.MarketplaceId, Ann, CancellationToken.None));
        Assert.NotNull(resolved.ResolvedAt);
    }

    [Fact]
    public async Task RefreshAsync_SkuOnlyAtHome_NeverAlerts()
    {
        _inventory.Positions.Add(new InventoryPosition { MarketplaceId = Us.MarketplaceId, Sku = "HOME", HomeStock = 5 });
        _inventory.Sold.Add(new UnitsSold("HOME", Now.AddDays(-29), 30));

        await CreateService().RefreshAsync(Us, CancellationToken.None);

        Assert.Empty(_repository.Alerts);
    }

    [Fact]
    public async Task ReadState_IsPerUser()
    {
        Selling("A", available: 0);
        Selling("B", available: 0);
        var service = CreateService();
        await service.RefreshAsync(Us, CancellationToken.None);

        var first = (await service.ListAsync(Us.MarketplaceId, Ann, CancellationToken.None))[0];
        await service.MarkReadAsync(Us.MarketplaceId, first.Id, Ann, CancellationToken.None);

        Assert.Equal(1, await service.CountUnreadAsync(Us.MarketplaceId, Ann, CancellationToken.None));
        Assert.Equal(2, await service.CountUnreadAsync(Us.MarketplaceId, Bob, CancellationToken.None));
        Assert.False((await service.ListAsync(Us.MarketplaceId, Ann, CancellationToken.None))[0].IsRead); // unread sorts first
    }
}
