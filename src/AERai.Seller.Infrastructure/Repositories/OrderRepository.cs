using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class OrderRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IOrderRepository
{
    public async Task<int> InsertNewOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var inserted = 0;
        foreach (var order in orders)
        {
            var exists = await dbContext.Orders.AnyAsync(o => o.AmazonOrderId == order.AmazonOrderId, cancellationToken);
            if (!exists)
            {
                dbContext.Orders.Add(order);
                inserted++;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return inserted;
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
}
