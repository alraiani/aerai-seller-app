namespace AERai.Seller.Domain;

public class OrderItem
{
    public int Id { get; set; }
    public required string AmazonOrderId { get; set; }
    public required string Sku { get; set; }
    public string? Asin { get; set; }
    public string? Title { get; set; }
    public int QuantityOrdered { get; set; }
    public decimal? ItemPrice { get; set; }
    public string? ItemPriceCurrency { get; set; }
}
