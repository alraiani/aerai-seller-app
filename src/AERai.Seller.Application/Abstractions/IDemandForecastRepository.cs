using AERai.Seller.Domain.Ai;

namespace AERai.Seller.Application.Abstractions;

public interface IDemandForecastRepository
{
    /// <summary>Append-only — forecasts are historized, never updated in place.</summary>
    Task InsertAsync(IEnumerable<DemandForecast> forecasts, CancellationToken cancellationToken = default);

    /// <summary>The most recent forecast per Sku (by ComputedAt).</summary>
    Task<IReadOnlyList<DemandForecast>> GetLatestPerSkuAsync(CancellationToken cancellationToken = default);
}
