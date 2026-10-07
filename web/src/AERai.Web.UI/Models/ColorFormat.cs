using AERai.Web.Domain.Core;

namespace AERai.Web.UI.Models;

/// <summary>Display helpers for <see cref="ProductColor"/>: names and the theme-aware CSS class.</summary>
public static class ColorFormat
{
    /// <summary>Every color, in palette order, for pickers.</summary>
    public static IReadOnlyList<ProductColor> All { get; } = Enum.GetValues<ProductColor>();

    /// <summary>Class that sets the <c>--sku</c> color token (see site.css), or empty for no color.</summary>
    /// <param name="color">The color.</param>
    /// <returns>e.g. "sku-color-blue".</returns>
    public static string CssClass(ProductColor? color) =>
        color is { } c ? $"sku-color-{c.ToString().ToLowerInvariant()}" : string.Empty;

    /// <summary>Display name.</summary>
    /// <param name="color">The color.</param>
    /// <returns>e.g. "Blue", or "No color".</returns>
    public static string Label(ProductColor? color) => color switch
    {
        null => "No color",
        ProductColor.Multi => "Multicolor",
        { } c => c.ToString(),
    };
}
