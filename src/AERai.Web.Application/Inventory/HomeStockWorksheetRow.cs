namespace AERai.Web.Application.Inventory;

/// <summary>One SKU's row as saved from the inventory worksheet.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Original">Home stock shown when the page loaded; the change and send are checked against it.</param>
/// <param name="Change">Units added (positive) or removed (negative); <see langword="null"/> or 0 means no change.</param>
/// <param name="Send">Units sent to Amazon from home stock; <see langword="null"/> or 0 means none.</param>
public sealed record HomeStockWorksheetRow(string Sku, int Original, int? Change, int? Send)
{
    /// <summary>Home stock once the change is applied, before any send.</summary>
    public int AfterChange => Original + (Change ?? 0);
}
