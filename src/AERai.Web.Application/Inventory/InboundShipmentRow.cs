using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>One Amazon shipment on the review page, with its SKUs.</summary>
/// <param name="Id">The shipment's id.</param>
/// <param name="ShipmentConfirmationId">The FBA shipment id.</param>
/// <param name="PlanName">The Send to Amazon plan's name.</param>
/// <param name="Name">The shipment's name.</param>
/// <param name="AmazonStatus">Amazon's shipment status.</param>
/// <param name="PlanCreatedAt">When the plan was created.</param>
/// <param name="FirstSeenAt">When the sync first saw the shipment.</param>
/// <param name="Review">Where the review stands.</param>
/// <param name="ReviewedAt">When the review last changed.</param>
/// <param name="ReviewedBy">Who changed it (<c>system</c> for the sync).</param>
/// <param name="Lines">The SKUs, those from home stock first.</param>
public sealed record InboundShipmentRow(
    long Id,
    string ShipmentConfirmationId,
    string? PlanName,
    string? Name,
    string AmazonStatus,
    DateTimeOffset? PlanCreatedAt,
    DateTimeOffset FirstSeenAt,
    InboundShipmentReview Review,
    DateTimeOffset? ReviewedAt,
    string? ReviewedBy,
    IReadOnlyList<InboundShipmentLine> Lines)
{
    /// <summary>Whether any SKU comes from home stock (otherwise there is nothing to log).</summary>
    public bool HasHomeStock => Lines.Any(l => l.FromHome);

    /// <summary>Whether the shipment waits for an accept or dismiss.</summary>
    public bool IsToReview => Review == InboundShipmentReview.ToReview && HasHomeStock;

    /// <summary>Units of the SKUs from home stock, as Amazon has them.</summary>
    public int HomeUnits => Lines.Where(l => l.FromHome).Sum(l => l.AmazonUnits);

    /// <summary>Units of the shipment's SKUs, from home or not.</summary>
    public int AmazonUnits => Lines.Sum(l => l.AmazonUnits);

    /// <summary>Units already logged as sent under the FBA id.</summary>
    public int LoggedUnits => Lines.Sum(l => l.LoggedUnits);

    /// <summary>Whether a logged shipment's SKUs no longer match what Amazon has in it.</summary>
    public bool DiffersFromAmazon => Lines.Any(l => l.DiffersFromAmazon);
}
