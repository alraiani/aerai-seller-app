using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Alerts;
using AERai.Web.Domain.Alerts;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for stock alerts: one open alert per SKU, and per-user read state.</summary>
public sealed class StockAlertRepositoryTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    private async Task AddProductAsync(string sku)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Products.Add(new Product { Sku = sku, Title = $"Title {sku}", CreatedAt = Now, UpdatedAt = Now });
        await db.SaveChangesAsync();
    }

    private static StockAlert Alert(string sku, StockAlertLevel level = StockAlertLevel.Out) =>
        new() { MarketplaceId = MarketplaceIds.UnitedStates, Sku = sku, Level = level, Message = $"{sku} alert", RaisedAt = Now, UpdatedAt = Now };

    private static StockAlertChanges Raise(params StockAlert[] alerts) => new(alerts, [], new Dictionary<long, string>());

    [SqlFact]
    public async Task ApplyAsync_SecondOpenAlertForSameSku_IsRejectedWithoutError()
    {
        await AddProductAsync("T-ALERT-1");
        await using var scope = fixture.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStockAlertRepository>();

        Assert.True(await repository.ApplyAsync(Raise(Alert("T-ALERT-1")), Now, CancellationToken.None));
        Assert.False(await repository.ApplyAsync(Raise(Alert("T-ALERT-1")), Now, CancellationToken.None));

        var open = Assert.Single(await repository.GetOpenAsync(MarketplaceIds.UnitedStates, CancellationToken.None), a => a.Sku == "T-ALERT-1");

        // Resolving and raising in one save is allowed (a change of level).
        Assert.True(await repository.ApplyAsync(new StockAlertChanges([Alert("T-ALERT-1", StockAlertLevel.Low)], [open.Id], new Dictionary<long, string>()), Now, CancellationToken.None));
        Assert.Equal(StockAlertLevel.Low, Assert.Single(await repository.GetOpenAsync(MarketplaceIds.UnitedStates, CancellationToken.None), a => a.Sku == "T-ALERT-1").Level);
    }

    [SqlFact]
    public async Task ReadState_IsPerUserAndMarkAllOnlyTouchesOpenAlerts()
    {
        await AddProductAsync("T-ALERT-2");
        await AddProductAsync("T-ALERT-3");
        await using var scope = fixture.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IStockAlertRepository>();
        await repository.ApplyAsync(Raise(Alert("T-ALERT-2"), Alert("T-ALERT-3")), Now, CancellationToken.None);
        var before = await repository.CountUnreadAsync(MarketplaceIds.UnitedStates, "bob@test", CancellationToken.None);

        var marked = await repository.MarkReadAsync(MarketplaceIds.UnitedStates, null, "ann@test", Now, CancellationToken.None);
        var again = await repository.MarkReadAsync(MarketplaceIds.UnitedStates, null, "ann@test", Now, CancellationToken.None);

        Assert.True(marked >= 2);
        Assert.Equal(0, again);
        Assert.Equal(0, await repository.CountUnreadAsync(MarketplaceIds.UnitedStates, "ann@test", CancellationToken.None));
        Assert.Equal(before, await repository.CountUnreadAsync(MarketplaceIds.UnitedStates, "bob@test", CancellationToken.None));
        Assert.Contains(await repository.ListAsync(MarketplaceIds.UnitedStates, "ann@test", Now.AddDays(-7), CancellationToken.None),
            v => v.Sku == "T-ALERT-2" && v.IsRead && v.Title == "Title T-ALERT-2");
    }
}
