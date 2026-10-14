using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IProductRepository"/> keyed by SKU.</summary>
internal sealed class FakeProductRepository : IProductRepository
{
    public Dictionary<string, ProductSummary> Products { get; } = new(StringComparer.Ordinal);

    /// <summary>Costs per (SKU, marketplace), as saved through <see cref="UpdateCostAsync"/> or <see cref="ApplyCostsAsync"/>.</summary>
    public Dictionary<(string Sku, string MarketplaceId), (decimal? Cost, DateTimeOffset UpdatedAt)> Costs { get; } = [];

    /// <summary>Every batch passed to <see cref="ApplyCostsAsync"/>.</summary>
    public List<IReadOnlyList<ProductCostChange>> AppliedBatches { get; } = [];

    /// <summary>Rows <see cref="ListForCostSheetAsync"/> returns.</summary>
    public List<ProductCostTemplateRow> SheetRows { get; } = [];

    public Task<bool> UpdateCostAsync(string sku, string marketplaceId, decimal? costOfGoods, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        if (!Products.ContainsKey(sku))
        {
            return Task.FromResult(false);
        }

        Costs[(sku, marketplaceId)] = (costOfGoods, updatedAt);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<string>> ApplyCostsAsync(string marketplaceId, IReadOnlyList<ProductCostChange> changes, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        AppliedBatches.Add(changes);
        var stale = new List<string>();
        foreach (var change in changes)
        {
            if (!Products.ContainsKey(change.Sku) || CostOf(change.Sku, marketplaceId) != change.Current)
            {
                stale.Add(change.Sku);
                continue;
            }

            Costs[(change.Sku, marketplaceId)] = (change.New, updatedAt);
        }

        return Task.FromResult<IReadOnlyList<string>>(stale);
    }

    public Task<IReadOnlyDictionary<string, ProductCostSnapshot>> GetCostsAsync(string marketplaceId, IReadOnlyCollection<string> skus, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, ProductCostSnapshot>>(
            skus.Where(Products.ContainsKey).ToDictionary(s => s, s => new ProductCostSnapshot(Products[s].Title, CostOf(s, marketplaceId)), StringComparer.Ordinal));

    public Task<ProductSummary?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult(Products.TryGetValue(sku, out var product)
            ? product with { CostOfGoods = CostOf(sku, marketplaceId) }
            : null);

    public Task<IReadOnlyList<ProductCostTemplateRow>> ListForCostSheetAsync(string marketplaceId, int? familyId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductCostTemplateRow>>(SheetRows);

    public Task<PagedResult<ProductSummary>> ListAsync(string marketplaceId, ProductListFilter filter, PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ProductListTotals> GetTotalsAsync(string marketplaceId, int? familyId, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();

    private decimal? CostOf(string sku, string marketplaceId) => Costs.GetValueOrDefault((sku, marketplaceId)).Cost;
}
