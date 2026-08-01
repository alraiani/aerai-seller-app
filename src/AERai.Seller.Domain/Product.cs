namespace AERai.Seller.Domain;

public class Product
{
    public required string Sku { get; set; }
    public string? Asin { get; set; }
    public string? Title { get; set; }
    public decimal? CostOfGoods { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
