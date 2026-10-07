using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IProductRepository"/> keyed by SKU.</summary>
internal sealed class FakeProductRepository : IProductRepository
{
    public Dictionary<string, ProductSummary> Products { get; } = new(StringComparer.Ordinal);

    /// <summary>Costs per (SKU, marketplace), as saved through <see cref="UpdateCostAsync"/>.</summary>
    public Dictionary<(string Sku, string MarketplaceId), (decimal? Cost, DateTimeOffset UpdatedAt)> Costs { get; } = [];

    public Task<bool> UpdateCostAsync(string sku, string marketplaceId, decimal? costOfGoods, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        if (!Products.ContainsKey(sku))
        {
            return Task.FromResult(false);
        }

        Costs[(sku, marketplaceId)] = (costOfGoods, updatedAt);
        return Task.FromResult(true);
    }

    public Task<ProductSummary?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult(Products.TryGetValue(sku, out var product)
            ? product with { CostOfGoods = Costs.GetValueOrDefault((sku, marketplaceId)).Cost }
            : null);

    public Task<PagedResult<ProductSummary>> ListAsync(string marketplaceId, PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
