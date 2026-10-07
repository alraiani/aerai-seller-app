namespace AERai.Web.Domain.Core;

/// <summary>
/// One line of a SKU's home-stock ledger in a marketplace: units in (positive) or out (negative),
/// when, and why. Entries are never edited or deleted; a mistake is undone by a reversing entry.
/// </summary>
/// <remarks>
/// The ledger is the history; <see cref="HomeStock"/> is its running balance. Both are written in the
/// same transaction, so the units of a SKU's entries always add up to its home stock.
/// </remarks>
public sealed class HomeStockMovement
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Marketplace the stock is held for.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>When the units physically moved (entered by the user; defaults to when it was logged).</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>What kind of movement this is.</summary>
    public HomeStockMovementType Type { get; set; }

    /// <summary>Units added (positive) or removed (negative); never zero.</summary>
    public int Units { get; set; }

    /// <summary>The SKU's home stock after this entry, in the order entries were logged.</summary>
    public int BalanceAfter { get; set; }

    /// <summary>Outside reference, e.g. a purchase order or FBA shipment id.</summary>
    public string? Reference { get; set; }

    /// <summary>Free-text note.</summary>
    public string? Note { get; set; }

    /// <summary>The entry this one reverses, if it is a reversal.</summary>
    public long? ReversesId { get; set; }

    /// <summary>When the entry was logged.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Email of the user who logged it.</summary>
    public required string CreatedBy { get; set; }
}
