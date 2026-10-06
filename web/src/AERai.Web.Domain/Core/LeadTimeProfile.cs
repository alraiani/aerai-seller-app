namespace AERai.Web.Domain.Core;

/// <summary>
/// A SKU's supply-chain timings in one marketplace, which Amazon has no visibility into (supplier →
/// prep → shipping to the fulfillment network), entered by users. Restock planning works backward
/// from the projected stockout date through these stages. Any field left null uses the app-wide
/// default; no row means every field does.
/// </summary>
/// <remarks>
/// Per marketplace because shipping to a US fulfillment center and to a Canadian one take
/// different times, and each marketplace may target a different amount of cover.
/// </remarks>
public sealed class LeadTimeProfile
{
    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Marketplace the timings apply to.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Days from placing a supplier order to the goods arriving.</summary>
    public int? SupplierLeadTimeDays { get; set; }

    /// <summary>Days to prepare arrived goods for shipping (labeling, packing).</summary>
    public int? PrepTimeDays { get; set; }

    /// <summary>Days from shipping to Amazon until the units are sellable (transit plus receiving).</summary>
    public int? TransitDays { get; set; }

    /// <summary>Extra days of cover kept in hand so restock lands before the projected stockout.</summary>
    public int? SafetyStockDays { get; set; }

    /// <summary>Days of sales a restock should cover; drives how much to send or order.</summary>
    public int? TargetStockDays { get; set; }

    /// <summary>When the profile was last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Email of the user who last changed it.</summary>
    public required string UpdatedBy { get; set; }
}
