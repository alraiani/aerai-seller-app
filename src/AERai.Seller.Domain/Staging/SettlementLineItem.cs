namespace AERai.Seller.Domain.Staging;

/// <summary>
/// One row from Amazon's settlement flat file (GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE), scoped to
/// a single SettlementReport via SettlementId. AmountType/AmountDescription are Amazon's own raw
/// "amount-type"/"amount-description" column values (e.g. "ItemFees"/"Commission") rather than a
/// curated enum, so bookkeeping account mapping (see BookkeepingAccountMapping) can key off exactly
/// what Amazon reports without the app hardcoding a classification scheme.
/// </summary>
public class SettlementLineItem
{
    public int Id { get; set; }
    public required string SettlementId { get; set; }
    public string? AmazonOrderId { get; set; }
    public string? Sku { get; set; }
    public required string AmountType { get; set; }
    public required string AmountDescription { get; set; }
    public decimal Amount { get; set; }
    public required string Currency { get; set; }
    public DateTimeOffset PostedDate { get; set; }
}
