using AERai.Seller.Domain;

namespace AERai.Seller.Application.Abstractions;

public interface IOrderRepository
{
    /// <summary>Inserts orders not already present (matched by AmazonOrderId); existing orders are left untouched. Returns the number newly inserted.</summary>
    Task<int> InsertNewOrdersAsync(IEnumerable<Order> orders, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Order>> GetOrdersPurchasedOnAsync(DateOnly date, CancellationToken cancellationToken = default);
}
