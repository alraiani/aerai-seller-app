using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IAmazonOrderRepository"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
/// <param name="alerts">Asked to refresh stock alerts, because orders drive each SKU's sales rate.</param>
internal sealed class AmazonOrderRepository(AppDbContext dbContext, IStockAlertRefreshSignal alerts) : IAmazonOrderRepository
{
    /// <summary>Marker for orders written by the Orders API, which have no staging batch.</summary>
    internal const long NoImportBatch = 0;

    /// <inheritdoc/>
    /// <remarks>An incoming <c>Unknown</c> status never replaces a status the order already has.</remarks>
    public async Task<OrderUpsertResult> UpsertAsync(string marketplaceId, IReadOnlyCollection<AmazonOrder> orders, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(orders);
        if (orders.Select(o => o.AmazonOrderId).Distinct(StringComparer.Ordinal).Count() != orders.Count)
        {
            throw new ArgumentException("Each order may appear only once.", nameof(orders));
        }

        if (orders.Count == 0)
        {
            return new OrderUpsertResult(0, 0);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            await EnsureProductsAsync(orders, syncedAt, cancellationToken).ConfigureAwait(false);

            var ids = orders.Select(o => o.AmazonOrderId).ToList();
            var existing = await dbContext.Orders
                .Include(o => o.Items)
                .Where(o => ids.Contains(o.AmazonOrderId))
                .ToDictionaryAsync(o => o.AmazonOrderId, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);

            var inserted = 0;
            foreach (var incoming in orders)
            {
                var currency = incoming.Currency is { Length: >= 3 } code ? code[..3].ToUpperInvariant() : null;
                if (existing.TryGetValue(incoming.AmazonOrderId, out var order))
                {
                    // An existing order keeps its marketplace: Amazon order ids are unique across marketplaces.
                    order.PurchaseDate = incoming.PurchaseDate;
                    // A page without a fulfillment block (an old raw file, or a request that did not ask for it)
                    // carries no status, and must not wipe one we already know.
                    if (incoming.Status != OrdersPageParser.UnknownStatus)
                    {
                        order.OrderStatus = Truncate(incoming.Status, 32);
                    }

                    order.Currency = currency ?? order.Currency;
                    order.LastImportBatchId = NoImportBatch;
                    order.UpdatedAt = syncedAt;
                    MergeItems(order, incoming);
                }
                else
                {
                    var created = new Order
                    {
                        AmazonOrderId = incoming.AmazonOrderId,
                        MarketplaceId = marketplaceId,
                        PurchaseDate = incoming.PurchaseDate,
                        OrderStatus = Truncate(incoming.Status, 32),
                        Currency = currency,
                        LastImportBatchId = NoImportBatch,
                        UpdatedAt = syncedAt,
                    };
                    MergeItems(created, incoming);
                    dbContext.Orders.Add(created);
                    inserted++;
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            dbContext.ChangeTracker.Clear();
            return new OrderUpsertResult(inserted, orders.Count - inserted);
        }).ConfigureAwait(false);

        alerts.Request();
        return result;
    }

    // Like promotion, items are updated or added by SKU and never removed.
    private static void MergeItems(Order order, AmazonOrder incoming)
    {
        foreach (var line in incoming.Lines)
        {
            var item = order.Items.FirstOrDefault(i => string.Equals(i.Sku, line.Sku, StringComparison.OrdinalIgnoreCase));
            if (item is null)
            {
                order.Items.Add(new OrderItem { Sku = line.Sku, Quantity = line.Quantity, ItemPrice = line.ItemPrice });
            }
            else
            {
                item.Quantity = line.Quantity;
                item.ItemPrice = line.ItemPrice;
            }
        }
    }

    /// <summary>Order items reference the shared catalog, so a SKU first seen on an order joins it, as in promotion.</summary>
    private async Task EnsureProductsAsync(IReadOnlyCollection<AmazonOrder> orders, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        var lines = orders.SelectMany(o => o.Lines).ToList();
        var skus = lines.Select(l => l.Sku).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var known = await dbContext.Products
            .Where(p => skus.Contains(p.Sku))
            .ToDictionaryAsync(p => p.Sku, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        foreach (var sku in skus)
        {
            var forSku = lines.Where(l => string.Equals(l.Sku, sku, StringComparison.OrdinalIgnoreCase)).ToList();
            var asin = forSku.Select(l => l.Asin).FirstOrDefault(a => !string.IsNullOrWhiteSpace(a));
            var title = forSku.Select(l => l.Title).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
            if (known.TryGetValue(sku, out var product))
            {
                // Fill gaps only; a curated title or ASIN is never overwritten by an order.
                var changed = false;
                if (string.IsNullOrWhiteSpace(product.Asin) && asin is not null)
                {
                    product.Asin = Truncate(asin, 16);
                    changed = true;
                }

                if (string.IsNullOrWhiteSpace(product.Title) && title is not null)
                {
                    product.Title = title;
                    changed = true;
                }

                if (changed)
                {
                    product.UpdatedAt = syncedAt;
                }
            }
            else
            {
                dbContext.Products.Add(new Product { Sku = sku, Asin = asin is null ? null : Truncate(asin, 16), Title = title, CreatedAt = syncedAt, UpdatedAt = syncedAt });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
