namespace AERai.Web.Domain.Core;

/// <summary>
/// A shipment into Amazon's fulfillment network, as the seller created it in Send to Amazon. One row per
/// FBA shipment id, written by the inbound shipments sync from the Fulfillment Inbound API.
/// </summary>
/// <remarks>
/// A shipment is a proposal, never a movement: home stock only changes once an Operator accepts it, which
/// logs a Shipped to Amazon send with the FBA shipment id as its reference (see <see cref="Review"/>).
/// </remarks>
public sealed class InboundShipment
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Marketplace whose schedule pulled the shipment (the one it ships into).</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>
    /// Amazon's shipment confirmation id, e.g. <c>FBA15ABCD1234</c>: the id Seller Central shows and the
    /// reference the ledger send carries. Unique per marketplace.
    /// </summary>
    public required string ShipmentConfirmationId { get; set; }

    /// <summary>Amazon's internal shipment id within its inbound plan (e.g. <c>sh…</c>).</summary>
    public required string AmazonShipmentId { get; set; }

    /// <summary>The inbound plan the shipment belongs to (e.g. <c>wf…</c>).</summary>
    public required string InboundPlanId { get; set; }

    /// <summary>The inbound plan's name, as the seller named it in Send to Amazon.</summary>
    public string? PlanName { get; set; }

    /// <summary>The shipment's name, when Amazon gives one.</summary>
    public string? Name { get; set; }

    /// <summary>Amazon's shipment status, e.g. <c>WORKING</c>, <c>SHIPPED</c>, <c>RECEIVING</c>, <c>CANCELLED</c>.</summary>
    public required string AmazonStatus { get; set; }

    /// <summary>When the inbound plan was created in Send to Amazon.</summary>
    public DateTimeOffset? PlanCreatedAt { get; set; }

    /// <summary>When the sync first saw the shipment.</summary>
    public DateTimeOffset FirstSeenAt { get; set; }

    /// <summary>When a sync last read the shipment from Amazon.</summary>
    public DateTimeOffset LastSeenAt { get; set; }

    /// <summary>Where the review stands.</summary>
    public InboundShipmentReview Review { get; set; } = InboundShipmentReview.ToReview;

    /// <summary>When the review last changed (accepted, dismissed, reopened, or decided by the sync).</summary>
    public DateTimeOffset? ReviewedAt { get; set; }

    /// <summary>Who changed the review last: a user's email, or <c>system</c> when the sync decided it.</summary>
    public string? ReviewedBy { get; set; }

    /// <summary>The SKUs in the shipment and their quantities, as Amazon lists them now.</summary>
    public List<InboundShipmentItem> Items { get; set; } = [];

    /// <summary>The <see cref="ReviewedBy"/> value written when the sync, not a user, decided the review.</summary>
    public const string SystemReviewer = "system";
}
