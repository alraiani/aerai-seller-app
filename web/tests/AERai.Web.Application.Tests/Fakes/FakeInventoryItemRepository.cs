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

    /// <summary>Lead-time overrides keyed by (marketplace, sku).</summary>
    public Dictionary<(string MarketplaceId, string Sku), LeadTimeSettings> LeadTimes { get; } = [];

    public int SaveCalls { get; private set; }

    public void AddProduct(string sku) => Products[sku] = new Product { Sku = sku };

    public Task<InventoryItemDetails?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult(Products.TryGetValue(sku, out var p)
            ? new InventoryItemDetails(p.Sku, p.Asin, p.Title, Families.FirstOrDefault(f => f.Id == p.FamilyId)?.Name, p.ImagePath,
                HomeStock.GetValueOrDefault((marketplaceId, sku)), LeadTimes.GetValueOrDefault((marketplaceId, sku)) ?? LeadTimeSettings.None)
            : null);

    public Task<IReadOnlyList<ProductFamily>> ListFamiliesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductFamily>>(Families.OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList());

    public Task<bool> SaveItemAsync(string sku, string marketplaceId, string? family, int homeStock, LeadTimeSettings leadTimes, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken)
    {
        if (!Products.TryGetValue(sku, out var product))
        {
            return Task.FromResult(false);
        }

        SaveCalls++;
        if (family is null)
        {
            product.FamilyId = null;
        }
        else
        {
            var existing = Families.FirstOrDefault(f => string.Equals(f.Name, family, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                existing = new ProductFamily { Id = Families.Count + 1, Name = family };
                Families.Add(existing);
            }

            product.FamilyId = existing.Id;
        }

        ApplyHomeStock(marketplaceId, [new HomeStockEntry(sku, homeStock)]);
        if (leadTimes.IsEmpty)
        {
            LeadTimes.Remove((marketplaceId, sku));
        }
        else
        {
            LeadTimes[(marketplaceId, sku)] = leadTimes;
        }

        return Task.FromResult(true);
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
        ApplyHomeStock(marketplaceId, entries);
        return Task.CompletedTask;
    }

    private void ApplyHomeStock(string marketplaceId, IEnumerable<HomeStockEntry> entries)
    {
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
    }
}
