using AERai.Seller.Domain;

namespace AERai.Seller.Application.Abstractions;

public interface IOrderRepository
{
    /// <summary>Idempotent upsert keyed by AmazonOrderId — safe to re-run for overlapping date ranges.</summary>
    Task UpsertOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetMostRecentPurchaseDateAsync(CancellationToken cancellationToken = default);
}
