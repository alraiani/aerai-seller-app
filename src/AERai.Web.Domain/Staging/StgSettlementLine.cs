namespace AERai.Web.Domain.Staging;

/// <summary>
/// A raw settlement flat-file line. The first line of a settlement file is a header row that
/// carries the period dates but no transaction type or amount type.
/// </summary>
public sealed class StgSettlementLine : StagingRow
{
    /// <summary><c>settlement-id</c>.</summary>
    public string? SettlementId { get; set; }

    /// <summary><c>settlement-start-date</c>.</summary>
    public string? SettlementStartDate { get; set; }

    /// <summary><c>settlement-end-date</c>.</summary>
    public string? SettlementEndDate { get; set; }

    /// <summary><c>posted-date</c>.</summary>
    public string? PostedDate { get; set; }

    /// <summary><c>transaction-type</c>, e.g. Order, Refund, ServiceFee.</summary>
    public string? TransactionType { get; set; }

    /// <summary><c>order-id</c>.</summary>
    public string? OrderId { get; set; }

    /// <summary><c>order-item-code</c>: identifies the order item the line belongs to.</summary>
    public string? OrderItemCode { get; set; }

    /// <summary><c>quantity-purchased</c>: units on the order item (present on its price lines only).</summary>
    public string? QuantityPurchased { get; set; }

    /// <summary><c>sku</c>.</summary>
    public string? Sku { get; set; }

    /// <summary><c>amount-type</c>, e.g. ItemPrice, ItemFees, Promotion.</summary>
    public string? AmountType { get; set; }

    /// <summary><c>amount-description</c>, e.g. Principal, FBAPerUnitFulfillmentFee.</summary>
    public string? AmountDescription { get; set; }

    /// <summary><c>amount</c> (signed; fees are negative).</summary>
    public string? Amount { get; set; }

    /// <summary><c>deposit-date</c> (header row only).</summary>
    public string? DepositDate { get; set; }

    /// <summary><c>currency</c>.</summary>
    public string? Currency { get; set; }
}
