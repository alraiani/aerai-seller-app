using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
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

    public Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken cancellationToken = default)
        => GetOrdersPurchasedBetweenAsync(date, date, cancellationToken);

    public async Task<IReadOnlyList<Order>> GetOrdersPurchasedBetweenAsync(DateOnly startDateInclusive, DateOnly endDateInclusive, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Neither a direct range comparison nor component-level (.Year/.Month/.Day) access
        // against a DateTimeOffset column translates on the current EF Core Sqlite provider —
        // filter client-side after materializing instead. Fine at this app's order volume;
        // revisit (e.g. a converted DateTime shadow column) if this table grows large.
        // Bounds are Pacific-Time calendar days (see AmazonBusinessDay), matching Amazon Seller
        // Central's "today" convention rather than UTC or local time.
        var orders = await dbContext.Orders
            .Include(o => o.Items)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return orders
            .Where(o =>
            {
                var day = AmazonBusinessDay.DateOf(o.PurchaseDate);
                return day >= startDateInclusive && day <= endDateInclusive;
            })
            .ToList();
    }

    public async Task<IReadOnlyList<OrderItem>> GetAllOrderItemsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.OrderItems.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task<DateOnly?> GetEarliestOrderDateAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        // Project only PurchaseDate (not the full order+items graph GetOrdersPurchasedBetweenAsync
        // loads) — cheaper, and still hits the same DateTimeOffset-translation gotcha, so Min()
        // is computed client-side after materializing.
        var purchaseDates = await dbContext.Orders.Select(o => o.PurchaseDate).ToListAsync(cancellationToken);
        if (purchaseDates.Count == 0) return null;

        return purchaseDates.Select(AmazonBusinessDay.DateOf).Min();
    }
}
