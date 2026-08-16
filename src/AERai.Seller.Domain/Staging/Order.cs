namespace AERai.Seller.Domain.Staging;

public class Order
{
    public required string AmazonOrderId { get; set; }
    public required string MarketplaceId { get; set; }
    public DateTimeOffset PurchaseDate { get; set; }
    public required string OrderStatus { get; set; }
    public decimal? OrderTotalAmount { get; set; }
    public string? OrderTotalCurrency { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }

    public List<OrderItem> Items { get; set; } = [];
}
