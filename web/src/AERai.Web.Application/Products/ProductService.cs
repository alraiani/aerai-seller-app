using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Products;

/// <summary>
/// Default <see cref="IProductService"/>.
/// </summary>
/// <param name="repository">Product persistence.</param>
/// <param name="timeProvider">Clock for the update timestamp.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ProductService(IProductRepository repository, TimeProvider timeProvider, ILogger<ProductService> logger)
    : IProductService
{
    /// <summary>Sanity ceiling on unit cost; anything higher is almost certainly a typo.</summary>
    public const decimal MaxCostOfGoods = 100_000m;

    /// <inheritdoc/>
    public async Task<Result> UpdateCostAsync(string sku, decimal? costOfGoods, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        if (costOfGoods is < 0 or > MaxCostOfGoods)
        {
            return Result.Failure($"Cost of goods must be between 0 and {MaxCostOfGoods:N0}.");
        }

        // Round to cents up front so the stored value matches what the user sees after save.
        var rounded = costOfGoods is null ? (decimal?)null : Math.Round(costOfGoods.Value, 2, MidpointRounding.AwayFromZero);

        var updated = await repository.UpdateCostAsync(sku, rounded, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return Result.Failure($"Product '{sku}' was not found.");
        }

        LogCostUpdated(sku, rounded);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated cost of goods for {Sku} to {CostOfGoods}")]
    private partial void LogCostUpdated(string sku, decimal? costOfGoods);
}
