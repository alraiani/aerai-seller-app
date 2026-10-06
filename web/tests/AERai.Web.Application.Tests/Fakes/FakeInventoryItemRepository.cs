using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IInventoryItemRepository"/> over a few seeded SKUs.</summary>
internal sealed class FakeInventoryItemRepository : IInventoryItemRepository
{
    public Dictionary<string, Product> Products { get; } = new(StringComparer.Ordinal);

    public List<ProductFamily> Families { get; } = [];

    /// <summary>Home stock keyed by (marketplace, sku).</summary>
    public Dictionary<(string MarketplaceId, string Sku), int> HomeStock { get; } = [];

    public int SaveCalls { get; private set; }

    public void AddProduct(string sku) => Products[sku] = new Product { Sku = sku };

    public Task<InventoryItemDetails?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult(Products.TryGetValue(sku, out var p)
            ? new InventoryItemDetails(p.Sku, p.Asin, p.Title, Families.FirstOrDefault(f => f.Id == p.FamilyId)?.Name, p.ImagePath, HomeStock.GetValueOrDefault((marketplaceId, sku)))
            : null);

    public Task<IReadOnlyList<ProductFamily>> ListFamiliesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductFamily>>(Families.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<int> GetOrCreateFamilyAsync(string name, CancellationToken cancellationToken)
    {
        var family = Families.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase));
        if (family is null)
        {
            family = new ProductFamily { Id = Families.Count + 1, Name = name };
            Families.Add(family);
        }

        return Task.FromResult(family.Id);
    }

    public Task<bool> SetFamilyAsync(string sku, int? familyId, CancellationToken cancellationToken)
    {
        if (!Products.TryGetValue(sku, out var p))
        {
            return Task.FromResult(false);
        }

        p.FamilyId = familyId;
        return Task.FromResult(true);
    }

    public Task DeleteUnusedFamiliesAsync(CancellationToken cancellationToken)
    {
        Families.RemoveAll(f => !Products.Values.Any(p => p.FamilyId == f.Id));
        return Task.CompletedTask;
    }

    public Task<(string Path, string ContentType)?> GetImageAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(Products.TryGetValue(sku, out var p) && p.ImagePath is { } path && p.ImageContentType is { } type
            ? ((string, string)?)(path, type)
            : null);

    public Task<bool> SetImageAsync(string sku, string? path, string? contentType, CancellationToken cancellationToken)
    {
        if (!Products.TryGetValue(sku, out var p))
        {
            return Task.FromResult(false);
        }

        (p.ImagePath, p.ImageContentType) = (path, contentType);
        return Task.FromResult(true);
    }

    public Task<IReadOnlySet<string>> GetExistingSkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<string>>(skus.Where(Products.ContainsKey).ToHashSet(StringComparer.Ordinal));

    public Task SetHomeStockAsync(string marketplaceId, IReadOnlyList<HomeStockEntry> entries, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken)
    {
        SaveCalls++;
        foreach (var entry in entries)
        {
            if (entry.Quantity == 0)
            {
                HomeStock.Remove((marketplaceId, entry.Sku));
            }
            else
            {
                HomeStock[(marketplaceId, entry.Sku)] = entry.Quantity;
            }
        }

        return Task.CompletedTask;
    }
}
