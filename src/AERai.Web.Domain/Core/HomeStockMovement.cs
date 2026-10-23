namespace AERai.Web.Domain.Core;

/// <summary>
/// One line of a SKU's home-stock ledger in a marketplace: units in (positive) or out (negative),
/// when, and why. Entries are never edited or deleted; a mistake is undone by a reversing entry, and an
/// edit made on the ledger page is a correction: the entry is reversed and a corrected copy logged
/// (<see cref="CorrectsId"/>), in one transaction.
/// </summary>
/// <remarks>
/// The ledger is the history; <see cref="HomeStock"/> is its running balance. Both are written in the
/// same transaction, so the units of a SKU's entries always add up to its home stock. Units and
/// balances never change after logging; the reconciliation fields (and a missing reference, once) are
/// bookkeeping about whether Amazon has caught up with a send, not edits to the ledger.
/// </remarks>
public sealed class HomeStockMovement
{
    /// <summary>The <see cref="ReconciledBy"/> value for sends matched automatically from Amazon's numbers.</summary>
    public const string SystemReconciler = "system";

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

    /// <summary>
    /// The entry this one corrects, if it was logged by editing that entry on the ledger page (which also
    /// reverses it). The page then lists only this copy; the original and its reversal stay as history.
    /// </summary>
    public long? CorrectsId { get; set; }

    /// <summary>
    /// For a <see cref="HomeStockMovementType.ShippedToAmazon"/> entry: the SKU's FBA sell-through stock
    /// (available, inbound, and FC transfers/processing) when the send was logged, the point Amazon's
    /// later numbers are compared with to see the units arrive. <see langword="null"/> for other entries
    /// and for sends logged before this was recorded.
    /// </summary>
    public int? AmazonBaselineUnits { get; set; }

    /// <summary>
    /// When the snapshot behind <see cref="AmazonBaselineUnits"/> was received, so sales since then can be
    /// told apart from units arriving; <see langword="null"/> when the SKU had no snapshot (nothing at Amazon).
    /// </summary>
    public DateTimeOffset? AmazonBaselineAt { get; set; }

    /// <summary>
    /// For a <see cref="HomeStockMovementType.ShippedToAmazon"/> entry: how many of its units Amazon's
    /// numbers already show. Until it reaches the units sent, the rest is still "pending to Amazon" and
    /// counts as Amazon stock for planning.
    /// </summary>
    public int ReconciledUnits { get; set; }

    /// <summary>When the send stopped being pending (fully matched, or marked reconciled by a user).</summary>
    public DateTimeOffset? ReconciledAt { get; set; }

    /// <summary>Who closed the send: a user's email, or <see cref="SystemReconciler"/> for automatic matching.</summary>
    public string? ReconciledBy { get; set; }

    /// <summary>When the entry was logged.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Email of the user who logged it.</summary>
    public required string CreatedBy { get; set; }
}
