namespace AERai.Web.UI.Models;

/// <summary>The inventory pages in the section tab bar (<c>_InventoryNav</c>), in display order.</summary>
public enum InventorySection
{
    /// <summary>The Inventory list (stock, cover, next step).</summary>
    Overview,

    /// <summary>The family worksheet (sales, sends, home stock, reorders).</summary>
    Worksheet,

    /// <summary>Sends from home that Amazon doesn't show yet.</summary>
    AwaitingAmazon,

    /// <summary>The home-stock ledger.</summary>
    Ledger,

    /// <summary>Product pictures.</summary>
    Pictures,
}
