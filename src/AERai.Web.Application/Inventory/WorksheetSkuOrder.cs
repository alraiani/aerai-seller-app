namespace AERai.Web.Application.Inventory;

/// <summary>
/// The order SKUs are listed in inside one color of a worksheet family, matching the printed count sheet:
/// styles the family carries in several colors first (A–Z, so FOB, FOG, HTS, KST, MOB, MOG, NUC, TOJ), then
/// one-off styles found in a single color (e.g. SBP001BL), also A–Z.
/// </summary>
public static class WorksheetSkuOrder
{
    /// <summary>A SKU's style code: the letters before its first digit (FOB00BL → FOB), or the whole SKU.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <returns>The style code.</returns>
    public static string Style(string sku)
    {
        ArgumentNullException.ThrowIfNull(sku);
        var digit = sku.AsSpan().IndexOfAnyInRange('0', '9');
        return digit > 0 ? sku[..digit] : sku;
    }

    /// <summary>The styles a family carries in more than one color.</summary>
    /// <param name="skus">The family's SKUs with their colors.</param>
    /// <returns>Those style codes.</returns>
    public static IReadOnlySet<string> SharedStyles<TColor>(IEnumerable<(string Sku, TColor Color)> skus)
    {
        ArgumentNullException.ThrowIfNull(skus);
        return skus
            .GroupBy(s => Style(s.Sku), StringComparer.Ordinal)
            .Where(g => g.Select(s => s.Color).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Orders one color's items: shared styles first, then one-off styles, each A–Z by SKU.</summary>
    /// <typeparam name="T">Item type.</typeparam>
    /// <param name="items">The items of one color.</param>
    /// <param name="sku">Reads an item's SKU.</param>
    /// <param name="sharedStyles">The family's styles carried in several colors (<see cref="SharedStyles"/>).</param>
    /// <returns>The items in sheet order.</returns>
    public static IEnumerable<T> Order<T>(IEnumerable<T> items, Func<T, string> sku, IReadOnlySet<string> sharedStyles)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(sku);
        ArgumentNullException.ThrowIfNull(sharedStyles);
        return items
            .OrderBy(i => sharedStyles.Contains(Style(sku(i))) ? 0 : 1)
            .ThenBy(sku, StringComparer.Ordinal);
    }
}
