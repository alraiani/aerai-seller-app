using AERai.Web.Domain.Core;

namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_InventoryPosition</c>: each SKU's latest snapshot in one marketplace,
/// pivoted into one column per inventory state. Sales velocity is computed by the application.
/// </summary>
public sealed class InventoryPosition
{
    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Marketplace the position is for.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>ASIN, when known.</summary>
    public string? Asin { get; set; }

    /// <summary>Product title, when known.</summary>
    public string? Title { get; set; }

    /// <summary>The product's family, if assigned.</summary>
    public int? FamilyId { get; set; }

    /// <summary>The family's name, if assigned.</summary>
    public string? Family { get; set; }

    /// <summary>Blob path of the product picture, if one was uploaded (changes with every upload).</summary>
    public string? ImagePath { get; set; }

    /// <summary>The variant's color, if set.</summary>
    public ProductColor? Color { get; set; }

    /// <summary>
    /// Units Amazon's latest restock report recommends sending in, or <see langword="null"/> when no
    /// report covers the SKU.
    /// </summary>
    public int? AmazonRecommendedQuantity { get; set; }

    /// <summary>The date Amazon recommends shipping by, when it gives one.</summary>
    public DateOnly? AmazonRecommendedShipDate { get; set; }

    /// <summary>
    /// Date of the most recent Amazon snapshot for the SKU, or <see langword="null"/> when the SKU is
    /// only held at home.
    /// </summary>
    public DateOnly? SnapshotDate { get; set; }

    /// <summary>Units sellable now.</summary>
    public int Available { get; set; }

    /// <summary>Units on a shipment plan that has not shipped yet.</summary>
    public int InboundWorking { get; set; }

    /// <summary>Units in transit to the fulfillment network.</summary>
    public int InboundShipped { get; set; }

    /// <summary>Units being received at a fulfillment center.</summary>
    public int InboundReceiving { get; set; }

    /// <summary>Inbound units whose stage is unknown (sources without a breakdown).</summary>
    public int InboundUnsplit { get; set; }

    /// <summary>Units reserved for customer orders that have not shipped (already sold).</summary>
    public int ReservedCustomerOrder { get; set; }

    /// <summary>Units moving between fulfillment centers.</summary>
    public int ReservedFcTransfer { get; set; }

    /// <summary>Units held for processing at a fulfillment center.</summary>
    public int ReservedFcProcessing { get; set; }

    /// <summary>Reserved units with no breakdown (no reserved-inventory report for the date).</summary>
    public int ReservedUnsplit { get; set; }

    /// <summary>Units not sellable.</summary>
    public int Unfulfillable { get; set; }

    /// <summary>Units held outside Amazon for this marketplace (entered by users).</summary>
    public int HomeStock { get; set; }

    /// <summary>All inbound units, whatever their stage.</summary>
    public int Inbound => InboundWorking + InboundShipped + InboundReceiving + InboundUnsplit;

    /// <summary>All reserved units, whatever the reason.</summary>
    public int Reserved => ReservedCustomerOrder + ReservedFcTransfer + ReservedFcProcessing + ReservedUnsplit;

    /// <summary>Whether <see cref="Inbound"/> is fully broken down by stage.</summary>
    public bool HasInboundBreakdown => InboundUnsplit == 0;

    /// <summary>Whether <see cref="Reserved"/> is fully broken down by reason.</summary>
    public bool HasReservedBreakdown => ReservedUnsplit == 0;

    /// <summary>Every unit Amazon reports for the SKU, in any state.</summary>
    public int AmazonTotal => Available + Inbound + Reserved + Unfulfillable;

    /// <summary>Every unit the seller owns: everything at Amazon plus home stock.</summary>
    public int OverallTotal => AmazonTotal + HomeStock;

    /// <summary>
    /// Units that will become (or already are) sellable without action: available, inbound, and
    /// stock moving or being processed between fulfillment centers.
    /// </summary>
    /// <remarks>
    /// Customer-order reservations are excluded because those units are already sold, and
    /// unfulfillable units never sell. Home stock is excluded because it is not at Amazon yet; it is
    /// what restock planning sends from. Unsplit reserved stock is excluded too: without a breakdown
    /// it may be mostly customer orders, so counting it would overstate cover.
    /// </remarks>
    public int SellThroughStock => Available + Inbound + ReservedFcTransfer + ReservedFcProcessing;
}
