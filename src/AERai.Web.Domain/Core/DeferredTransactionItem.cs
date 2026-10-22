namespace AERai.Web.Domain.Core;

/// <summary>One product on a <see cref="DeferredTransaction"/>, as Amazon lists it.</summary>
public sealed class DeferredTransactionItem
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>The transaction the item belongs to.</summary>
    public required string TransactionId { get; set; }

    /// <summary>Seller SKU, when Amazon gave one.</summary>
    public string? Sku { get; set; }

    /// <summary>ASIN, when Amazon gave one.</summary>
    public string? Asin { get; set; }

    /// <summary>Units shipped.</summary>
    public int Quantity { get; set; }

    /// <summary>The item's product charges (its price times quantity), for its price each.</summary>
    public decimal Principal { get; set; }

    /// <summary>The item's total: what will be paid out for it after Amazon's fees (signed).</summary>
    public decimal Amount { get; set; }
}
