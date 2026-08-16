using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain.Staging;
using AERai.Seller.SpApiClient.CatalogItems;
using Microsoft.Extensions.Logging;

namespace AERai.Seller.Application.Sync;

public interface ICatalogSyncService
{
    Task SyncAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// Pulls catalog data (title, brand, image, parent ASIN) for every ASIN we know about — from
/// synced Products and from OrderItems (Products isn't populated by any sync job yet, so
/// OrderItems is the primary source of known ASINs today) — via the Catalog Items API, then
/// fetches each referenced parent ASIN too so <see cref="CatalogParent"/> rows carry a family
/// title. Unlike the Reports-API-backed syncs, this calls a REST API directly through
/// <see cref="CatalogItemsApiClient"/> in batches of up to
/// <see cref="CatalogItemsApiClient.MaxIdentifiersPerRequest"/> ASINs.
/// </summary>
public sealed class CatalogSyncService(
    CatalogItemsApiClient catalogItemsApiClient,
    IProductRepository productRepository,
    IOrderRepository orderRepository,
    ICatalogRepository catalogRepository,
    ISyncMetadataRepository syncMetadataRepository,
    TimeProvider timeProvider,
    ILogger<CatalogSyncService> logger) : ICatalogSyncService
{
    public const string SyncJobName = "Catalog";

    public async Task SyncAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        try
        {
            progress?.Report("Catalog: gathering known ASINs...");
            var products = await productRepository.GetAllAsync(cancellationToken);
            var orderItems = await orderRepository.GetAllOrderItemsAsync(cancellationToken);

            var asinToSku = new Dictionary<string, string?>();
            foreach (var product in products.Where(p => !string.IsNullOrEmpty(p.Asin)))
            {
                asinToSku.TryAdd(product.Asin!, product.Sku);
            }
            foreach (var item in orderItems.Where(i => !string.IsNullOrEmpty(i.Asin)))
            {
                asinToSku.TryAdd(item.Asin!, item.Sku);
            }

            var now = timeProvider.GetUtcNow();
            var items = await FetchItemsAsync(asinToSku.Keys.ToList(), asinToSku, now, progress, cancellationToken);

            var parentAsins = items
                .Select(i => i.ParentAsin)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!)
                .Distinct()
                .Except(asinToSku.Keys)
                .ToList();

            var parentItems = await FetchItemsAsync(parentAsins, sku: null, now, progress, cancellationToken);
            var titleByAsin = items.Concat(parentItems).ToDictionary(i => i.Asin, i => i.Title);

            var parents = items
                .Select(i => i.ParentAsin)
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => p!)
                .Distinct()
                .Select(p => new CatalogParent { ParentAsin = p, Title = titleByAsin.GetValueOrDefault(p), SyncedAt = now })
                .ToList();

            await catalogRepository.UpsertAsync(items, parents, cancellationToken);
            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: true, errorMessage: null, cancellationToken);

            progress?.Report($"Catalog: done, {items.Count} item(s) across {parents.Count} parent group(s) synced.");
            logger.LogInformation(
                "Catalog sync completed: {ItemCount} items, {ParentCount} parents upserted", items.Count, parents.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Catalog sync failed");
            await syncMetadataRepository.RecordResultAsync(SyncJobName, succeeded: false, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task<List<CatalogItem>> FetchItemsAsync(
        IReadOnlyList<string> asins,
        IReadOnlyDictionary<string, string?>? sku,
        DateTimeOffset syncedAt,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var items = new List<CatalogItem>();
        foreach (var batch in asins.Chunk(CatalogItemsApiClient.MaxIdentifiersPerRequest))
        {
            progress?.Report($"Catalog: fetching {batch.Length} item(s)...");
            var results = await catalogItemsApiClient.SearchCatalogItemsAsync(batch, cancellationToken);
            items.AddRange(results.Select(r => new CatalogItem
            {
                Asin = r.Asin,
                ParentAsin = r.ParentAsin,
                Sku = sku?.GetValueOrDefault(r.Asin),
                Title = r.Title,
                Brand = r.Brand,
                ImageUrl = r.ImageUrl,
                SyncedAt = syncedAt,
            }));
        }
        return items;
    }
}
