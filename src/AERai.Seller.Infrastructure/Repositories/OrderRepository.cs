using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class OrderRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IOrderRepository
{
    public async Task UpsertOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        foreach (var order in orders)
        {
            var existing = await dbContext.Orders
                .Include(o => o.Items)
                .SingleOrDefaultAsync(o => o.AmazonOrderId == order.AmazonOrderId, cancellationToken);

            if (existing is null)
            {
                dbContext.Orders.Add(order);
            }
            else
            {
                existing.OrderStatus = order.OrderStatus;
                existing.OrderTotalAmount = order.OrderTotalAmount;
                existing.OrderTotalCurrency = order.OrderTotalCurrency;
                existing.LastUpdatedAt = order.LastUpdatedAt;

                dbContext.OrderItems.RemoveRange(existing.Items);
                existing.Items = order.Items;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Neither a direct range comparison nor component-level (.Year/.Month/.Day) access
        // against a DateTimeOffset column translates on the current EF Core Sqlite provider —
        // filter client-side after materializing instead. Fine at this app's order volume;
        // revisit (e.g. a converted DateTime shadow column) if this table grows large.
        var orders = await dbContext.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return orders.Where(o => DateOnly.FromDateTime(o.PurchaseDate.UtcDateTime) == date).ToList();
    }

    public async Task<DateTimeOffset?> GetMostRecentPurchaseDateAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // MaxAsync over a DateTimeOffset column hits the same Sqlite-provider translation
        // limitation as the range/component filters above — only PurchaseDate values are
        // pulled (not full order graphs), so this stays cheap even at real order volumes.
        var purchaseDates = await dbContext.Orders.Select(o => o.PurchaseDate).ToListAsync(cancellationToken);
        return purchaseDates.Count == 0 ? null : purchaseDates.Max();
    }
}
