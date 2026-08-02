using AERai.Seller.Application.Abstractions;

namespace AERai.Seller.Application.Dashboard;

public sealed record SkuOrderSummary(string Sku, int UnitsSold, decimal Revenue, string Currency);

public sealed record SyncStatusSummary(string SyncJobName, DateTimeOffset? LastSuccessfulSyncAt, bool LastSyncSucceeded, string? LastErrorMessage);

public interface IDashboardQueryService
{
    Task<IReadOnlyList<SkuOrderSummary>> GetOrdersBySkuAsync(DateOnly date, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SyncStatusSummary>> GetSyncStatusAsync(CancellationToken cancellationToken = default);
}

/// <summary>Backs the Dashboard's top widget: a chosen day's orders summary grouped by SKU, plus last-sync status.</summary>
public sealed class DashboardQueryService(
    IOrderRepository orderRepository,
    ISyncMetadataRepository syncMetadataRepository) : IDashboardQueryService
{
    public async Task<IReadOnlyList<SkuOrderSummary>> GetOrdersBySkuAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        var orders = await orderRepository.GetOrdersPurchasedOnAsync(date, cancellationToken);

        return orders
            .SelectMany(o => o.Items.Select(i => (Item: i, Order: o)))
            .GroupBy(x => x.Item.Sku)
            .Select(g => new SkuOrderSummary(
                Sku: g.Key,
                UnitsSold: g.Sum(x => x.Item.QuantityOrdered),
                Revenue: g.Sum(x => (x.Item.ItemPrice ?? 0m) * x.Item.QuantityOrdered),
                Currency: g.Select(x => x.Item.ItemPriceCurrency).FirstOrDefault(c => c is not null) ?? "USD"))
            .OrderByDescending(s => s.Revenue)
            .ToList();
    }

    public async Task<IReadOnlyList<SyncStatusSummary>> GetSyncStatusAsync(CancellationToken cancellationToken = default)
    {
        var records = await syncMetadataRepository.GetAllAsync(cancellationToken);
        return records
            .Select(r => new SyncStatusSummary(r.SyncJobName, r.LastSuccessfulSyncAt, r.LastSyncSucceeded, r.LastErrorMessage))
            .ToList();
    }
}
