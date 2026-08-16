namespace AERai.Seller.Domain.Staging;

public class SettlementReport
{
    public required string SettlementId { get; set; }
    public required string MarketplaceId { get; set; }
    public DateTimeOffset FinancialEventGroupStart { get; set; }
    public DateTimeOffset FinancialEventGroupEnd { get; set; }
    public decimal TotalAmount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset? DepositDate { get; set; }
}
