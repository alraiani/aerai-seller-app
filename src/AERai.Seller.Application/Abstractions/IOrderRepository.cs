using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Abstractions;

public interface IOrderRepository
{
    /// <summary>Inserts orders not already present (matched by AmazonOrderId); existing orders are left untouched. Returns the number newly inserted.</summary>
    Task<int> InsertNewOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetOrdersPurchasedBetweenAsync(DateOnly startDateInclusive, DateOnly endDateInclusive, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OrderItem>> GetAllOrderItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>The purchase date of the oldest known order, or null if there are none yet. Used to detect thin order history.</summary>
    Task<DateOnly?> GetEarliestOrderDateAsync(CancellationToken cancellationToken = default);
}
