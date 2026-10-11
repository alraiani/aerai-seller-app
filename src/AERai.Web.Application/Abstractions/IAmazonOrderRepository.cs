using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>Writes orders pulled from the Orders API straight to <c>core.Order</c> and <c>core.OrderItem</c>.</summary>
public interface IAmazonOrderRepository
{
    /// <summary>
    /// Inserts new orders and refreshes existing ones (status, purchase date, currency, and line items),
    /// in one transaction. SKUs not yet in the product catalog are added to it. An existing order keeps
    /// its marketplace, as Amazon order ids are unique across marketplaces.
    /// </summary>
    /// <remarks>
    /// Like promotion, this never removes items an order already has, and it asks for a stock-alert
    /// refresh because orders drive each SKU's sales rate.
    /// </remarks>
    /// <param name="marketplaceId">Marketplace whose schedule pulled the orders.</param>
    /// <param name="orders">The orders; an order id may appear only once.</param>
    /// <param name="syncedAt">When the orders were pulled.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many orders were inserted and updated.</returns>
    Task<OrderUpsertResult> UpsertAsync(string marketplaceId, IReadOnlyCollection<AmazonOrder> orders, DateTimeOffset syncedAt, CancellationToken cancellationToken);
}
