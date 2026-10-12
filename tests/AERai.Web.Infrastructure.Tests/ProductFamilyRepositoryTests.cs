using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>SQL-backed tests for product family management.</summary>
public sealed class ProductFamilyRepositoryTests(SqlDatabaseFixture fixture) : IClassFixture<SqlDatabaseFixture>
{
    private async Task AddProductsAsync(params string[] skus)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        foreach (var sku in skus)
        {
            db.Products.Add(new Product { Sku = sku, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        }

        await db.SaveChangesAsync();
    }

    [SqlFact]
    public async Task CreateRenameAssignDelete_RoundTrip()
    {
        await AddProductsAsync("T-FAMREPO-1", "T-FAMREPO-2");
        await using var scope = fixture.Services.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IProductFamilyRepository>();

        var id = await repository.CreateAsync("Repo mats", CancellationToken.None);
        Assert.NotNull(id);
        Assert.Null(await repository.CreateAsync("REPO MATS", CancellationToken.None)); // unique ignoring case
        Assert.Equal(id, await repository.FindAsync("repo mats", CancellationToken.None));

        Assert.Equal(2, await repository.AssignAsync(["T-FAMREPO-1", "T-FAMREPO-2", "T-NOT-THERE"], id, CancellationToken.None));
        Assert.Equal(2, (await repository.ListAsync(CancellationToken.None)).Single(f => f.Id == id).SkuCount);

        var other = await repository.CreateAsync("Repo straps", CancellationToken.None);
        Assert.Equal(FamilyWriteOutcome.Duplicate, await repository.RenameAsync(other!.Value, "repo MATS", CancellationToken.None));
        Assert.Equal(FamilyWriteOutcome.Saved, await repository.RenameAsync(id!.Value, "Repo yoga mats", CancellationToken.None));
        Assert.Equal(FamilyWriteOutcome.NotFound, await repository.RenameAsync(-1, "Nope", CancellationToken.None));

        Assert.Equal("Repo yoga mats", await repository.DeleteAsync(id.Value, CancellationToken.None));
        Assert.Null(await repository.DeleteAsync(id.Value, CancellationToken.None));
        var items = scope.ServiceProvider.GetRequiredService<IInventoryItemRepository>();
        Assert.Null((await items.GetAsync("T-FAMREPO-1", MarketplaceIds.UnitedStates, CancellationToken.None))!.Family); // unassigned, not deleted
    }
}
