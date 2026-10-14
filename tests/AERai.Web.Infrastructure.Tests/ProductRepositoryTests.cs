using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for the Products list (filters, sorts, totals) and bulk cost saves.</summary>
public sealed class ProductRepositoryTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private const string Us = MarketplaceIds.UnitedStates;
    private const string Ca = MarketplaceIds.Canada;

    /// <summary>Adds products whose SKUs share a prefix unique to the test, so tests can't see each other's rows.</summary>
    private async Task<int> SeedAsync(string prefix)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var family = new ProductFamily { Name = prefix + " Mats" };
        db.ProductFamilies.Add(family);
        await db.SaveChangesAsync();

        var at = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        db.Products.AddRange(
            new Product { Sku = prefix + "-A", Title = "Yoga mat blue", FamilyId = family.Id, CreatedAt = at, UpdatedAt = at },
            new Product { Sku = prefix + "-B", Title = "Strap black", CreatedAt = at, UpdatedAt = at },
            new Product { Sku = prefix + "-C", Title = "Yoga mat black", FamilyId = family.Id, CreatedAt = at, UpdatedAt = at });
        db.ProductCosts.AddRange(
            new ProductCost { Sku = prefix + "-A", MarketplaceId = Us, CostOfGoods = 3m, UpdatedAt = at.AddDays(1) },
            new ProductCost { Sku = prefix + "-B", MarketplaceId = Us, CostOfGoods = 9m, UpdatedAt = at.AddDays(2) },
            new ProductCost { Sku = prefix + "-C", MarketplaceId = Ca, CostOfGoods = 1m, UpdatedAt = at });
        await db.SaveChangesAsync();
        return family.Id;
    }

    private static async Task<List<string>> SkusAsync(IProductRepository products, ProductListFilter filter, string search) =>
        (await products.ListAsync(Us, filter, new PageRequest(1, 50, search), CancellationToken.None)).Items.Select(p => p.Sku).ToList();

    [SqlFact]
    public async Task ListAsync_FiltersSearchesAndSorts()
    {
        var familyId = await SeedAsync("T-PRL");
        await using var scope = fixture.Services.CreateAsyncScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        Assert.Equal(["T-PRL-C"], await SkusAsync(products, new(ProductCostFilter.Missing), "T-PRL"));
        Assert.Equal(["T-PRL-A", "T-PRL-B"], await SkusAsync(products, new(ProductCostFilter.Set), "T-PRL"));
        Assert.Equal(["T-PRL-A", "T-PRL-C"], await SkusAsync(products, new(FamilyId: familyId), "T-PRL"));
        Assert.Equal(["T-PRL-C"], await SkusAsync(products, ProductListFilter.Default, "black t-prl mat"));
        Assert.Equal(["T-PRL-A", "T-PRL-C"], await SkusAsync(products, ProductListFilter.Default, "T-PRL mats"));

        // Highest cost first; no cost always last, whichever direction.
        Assert.Equal(["T-PRL-B", "T-PRL-A", "T-PRL-C"], await SkusAsync(products, new(Sort: ProductSort.Cost), "T-PRL"));
        Assert.Equal(["T-PRL-A", "T-PRL-B", "T-PRL-C"], await SkusAsync(products, new(Sort: ProductSort.Cost, Descending: true), "T-PRL"));
        Assert.Equal(["T-PRL-B", "T-PRL-A", "T-PRL-C"], await SkusAsync(products, new(Sort: ProductSort.Updated), "T-PRL"));
        Assert.Equal(["T-PRL-C", "T-PRL-B", "T-PRL-A"], await SkusAsync(products, new(Sort: ProductSort.Sku, Descending: true), "T-PRL"));
        Assert.Equal(["T-PRL-A", "T-PRL-C", "T-PRL-B"], await SkusAsync(products, new(Sort: ProductSort.Family), "T-PRL"));

        var first = (await products.ListAsync(Us, ProductListFilter.Default, new PageRequest(1, 50, "T-PRL-A"), CancellationToken.None)).Items.Single();
        Assert.Equal(("T-PRL Mats", 3m), (first.Family, first.CostOfGoods));

        var totals = await products.GetTotalsAsync(Us, null, "T-PRL", CancellationToken.None);
        Assert.Equal((3, 2, 1), (totals.SkuCount, totals.WithCost, totals.MissingCost));
        Assert.Equal(0, (await products.GetTotalsAsync(Us, null, "T-PRL-NONE", CancellationToken.None)).SkuCount);

        var sheet = await products.ListForCostSheetAsync(Us, familyId, CancellationToken.None);
        Assert.Equal([("T-PRL-A", (decimal?)3m), ("T-PRL-C", null)], sheet.Select(r => (r.Sku, r.CostOfGoods)));
    }

    [SqlFact]
    public async Task ApplyCostsAsync_SetsChangesAndClears_SkippingStaleAndUnknown()
    {
        await SeedAsync("T-PRA");
        var at = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        await using var scope = fixture.Services.CreateAsyncScope();
        var products = scope.ServiceProvider.GetRequiredService<IProductRepository>();

        var stale = await products.ApplyCostsAsync(
            Us,
            [
                new("T-PRA-A", 3m, null),   // clear
                new("T-PRA-B", 1m, 5m),     // stale: is 9
                new("T-PRA-C", null, 2.5m), // new (CA's cost doesn't count)
                new("T-PRA-Z", null, 1m),   // unknown SKU
            ],
            at,
            CancellationToken.None);

        Assert.Equal(["T-PRA-B", "T-PRA-Z"], stale);
        var costs = await products.GetCostsAsync(Us, ["T-PRA-A", "T-PRA-B", "T-PRA-C", "T-PRA-Z"], CancellationToken.None);
        Assert.Equal([("T-PRA-A", (decimal?)null), ("T-PRA-B", 9m), ("T-PRA-C", 2.5m)], costs.OrderBy(c => c.Key).Select(c => (c.Key, c.Value.CostOfGoods)));
        Assert.Equal(at, (await products.GetAsync("T-PRA-C", Us, CancellationToken.None))!.CostUpdatedAt);
        Assert.Equal(1m, (await products.GetAsync("T-PRA-C", Ca, CancellationToken.None))!.CostOfGoods);
    }
}
