namespace AERai.Web.Domain.Core;

/// <summary>
/// A user-defined group of similar products (e.g. "Yoga mats"), used to search and total inventory
/// by category. Shared by every marketplace, like <see cref="Product"/>.
/// </summary>
public sealed class ProductFamily
{
    /// <summary>Surrogate key.</summary>
    public int Id { get; set; }

    /// <summary>Display name; unique, case-insensitively.</summary>
    public required string Name { get; set; }

    /// <summary>Sends to Amazon are a multiple of this many units (e.g. 5); null = not rounded.</summary>
    public int? SendMultiple { get; set; }

    /// <summary>From <see cref="SendLargeFrom"/> units up, sends are a multiple of this instead (e.g. 10).</summary>
    public int? SendMultipleLarge { get; set; }

    /// <summary>The send size where <see cref="SendMultipleLarge"/> takes over (e.g. 50).</summary>
    public int? SendLargeFrom { get; set; }

    /// <summary>Supplier orders are at least this many units (e.g. 100); null = no minimum.</summary>
    public int? OrderMinimum { get; set; }

    /// <summary>Supplier orders are a multiple of this many units (e.g. 10); null = not rounded.</summary>
    public int? OrderMultiple { get; set; }

    /// <summary>
    /// Whether the family's units go into Amazon from home stock, so an Amazon shipment holding them is offered for
    /// review as a Shipped to Amazon send (Inventory → Amazon shipments). Off: its SKUs are shown but never subtracted.
    /// </summary>
    public bool ShipsFromHomeStock { get; set; }
}
