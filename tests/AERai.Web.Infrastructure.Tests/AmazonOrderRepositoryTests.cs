using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for orders written straight from the Orders API.</summary>
public sealed class AmazonOrderRepositoryTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    private static AmazonOrder Order(string id, string status, params AmazonOrderLine[] lines) =>
        new(id, Now.AddHours(-6), Now.AddHours(-1), status, "USD", lines);

    private async Task<OrderUpsertResult> UpsertAsync(string marketplaceId, params AmazonOrder[] orders)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IAmazonOrderRepository>().UpsertAsync(marketplaceId, orders, Now, CancellationToken.None);
    }

    [SqlFact]
    public async Task UpsertAsync_NewOrder_IsInsertedWithItemsAndItsSkuJoinsTheCatalog()
    {
        var result = await UpsertAsync(MarketplaceIds.UnitedStates, Order("T-API-1", "Shipped", new AmazonOrderLine("T-API-SKU", "B0TESTAPI1", "Test widget", 2, 19.98m)));

        Assert.Equal(new OrderUpsertResult(1, 0), result);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.AmazonOrderId == "T-API-1");
        Assert.Equal((MarketplaceIds.UnitedStates, "Shipped", "USD"), (order.MarketplaceId, order.OrderStatus, order.Currency));
        var item = Assert.Single(order.Items);
        Assert.Equal(("T-API-SKU", 2, 19.98m), (item.Sku, item.Quantity, item.ItemPrice));
        var product = await db.Products.AsNoTracking().SingleAsync(p => p.Sku == "T-API-SKU");
        Assert.Equal(("B0TESTAPI1", "Test widget"), (product.Asin, product.Title));
    }

    [SqlFact]
    public async Task UpsertAsync_ExistingOrder_RefreshesStatusAndItemsButKeepsMarketplaceAndOtherItems()
    {
        await UpsertAsync(MarketplaceIds.UnitedStates, Order("T-API-2", "Pending", new AmazonOrderLine("T-API-A", null, null, 1, 0m), new AmazonOrderLine("T-API-B", null, null, 1, 5m)));

        var result = await UpsertAsync(MarketplaceIds.Canada, Order("T-API-2", "Cancelled", new AmazonOrderLine("T-API-A", null, null, 3, 30m)));

        Assert.Equal(new OrderUpsertResult(0, 1), result);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var order = await db.Orders.AsNoTracking().Include(o => o.Items).SingleAsync(o => o.AmazonOrderId == "T-API-2");

        // Amazon order ids are unique across marketplaces, so the first marketplace stays.
        Assert.Equal((MarketplaceIds.UnitedStates, "Cancelled"), (order.MarketplaceId, order.OrderStatus));
        Assert.Equal(2, order.Items.Count);
        var changed = Assert.Single(order.Items, i => i.Sku == "T-API-A");
        Assert.Equal((3, 30m), (changed.Quantity, changed.ItemPrice));
        Assert.Contains(order.Items, i => i.Sku == "T-API-B");
    }

    [SqlFact]
    public async Task UpsertAsync_ExistingProduct_KeepsItsCuratedTitleAndAsin()
    {
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Products.Add(new Product { Sku = "T-API-CURATED", Asin = "B0CURATED1", Title = "Curated title", CreatedAt = Now, UpdatedAt = Now });
            await db.SaveChangesAsync();
        }

        await UpsertAsync(MarketplaceIds.UnitedStates, Order("T-API-3", "Shipped", new AmazonOrderLine("T-API-CURATED", "B0OTHER001", "Amazon's title", 1, 9m)));

        await using var verify = fixture.Services.CreateAsyncScope();
        var product = await verify.ServiceProvider.GetRequiredService<AppDbContext>().Products.AsNoTracking().SingleAsync(p => p.Sku == "T-API-CURATED");
        Assert.Equal(("B0CURATED1", "Curated title"), (product.Asin, product.Title));
    }

    [SqlFact]
    public async Task UpsertAsync_RepeatedOrderId_IsRejected()
    {
        var order = Order("T-API-4", "Shipped", new AmazonOrderLine("T-API-SKU", null, null, 1, 1m));

        await Assert.ThrowsAsync<ArgumentException>(() => UpsertAsync(MarketplaceIds.UnitedStates, order, order));
    }
}
