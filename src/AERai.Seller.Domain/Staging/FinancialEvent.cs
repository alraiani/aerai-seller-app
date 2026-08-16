namespace AERai.Seller.Domain.Staging;

public class FinancialEvent
{
    public int Id { get; set; }
    public string? AmazonOrderId { get; set; }
    public string? Sku { get; set; }
    public FinancialEventType EventType { get; set; }
    public required string EventSubType { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset PostedDate { get; set; }
    public string? Description { get; set; }
}
