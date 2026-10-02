using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IProductRepository"/> keyed by SKU.</summary>
internal sealed class FakeProductRepository : IProductRepository
{
    public Dictionary<string, ProductSummary> Products { get; } = new(StringComparer.Ordinal);

    public Task<bool> UpdateCostAsync(string sku, decimal? costOfGoods, DateTimeOffset updatedAt, CancellationToken cancellationToken)
    {
        if (!Products.TryGetValue(sku, out var existing))
        {
            return Task.FromResult(false);
        }

        Products[sku] = existing with { CostOfGoods = costOfGoods, UpdatedAt = updatedAt };
        return Task.FromResult(true);
    }

    public Task<ProductSummary?> GetAsync(string sku, CancellationToken cancellationToken) =>
        Task.FromResult(Products.GetValueOrDefault(sku));

    public Task<PagedResult<ProductSummary>> ListAsync(PageRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
