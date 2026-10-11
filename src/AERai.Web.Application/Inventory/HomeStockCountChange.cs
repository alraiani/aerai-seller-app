using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>One SKU whose uploaded count differs from its home stock.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Current">Home stock when the upload was checked.</param>
/// <param name="New">The count in the file.</param>
/// <param name="Title">Product title, for display.</param>
/// <param name="Color">The SKU's color, for display.</param>
public sealed record HomeStockCountChange(string Sku, int Current, int New, string? Title = null, ProductColor? Color = null)
{
    /// <summary>Units to add (positive) or remove (negative).</summary>
    public int Difference => New - Current;
}
