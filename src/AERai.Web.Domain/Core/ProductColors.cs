namespace AERai.Web.Domain.Core;

/// <summary>The order product colors are listed in, which is not their stored value.</summary>
public static class ProductColors
{
    /// <summary>
    /// Every color in display order: the order the printed handkerchief count sheet uses (blue, green, pink,
    /// purple), with the rest after them. The enum's numbers are stored, so the order lives here instead.
    /// </summary>
    public static IReadOnlyList<ProductColor> DisplayOrder { get; } =
    [
        ProductColor.Blue,
        ProductColor.Navy,
        ProductColor.Green,
        ProductColor.Teal,
        ProductColor.Pink,
        ProductColor.Purple,
        ProductColor.Red,
        ProductColor.Orange,
        ProductColor.Yellow,
        ProductColor.Brown,
        ProductColor.Beige,
        ProductColor.Black,
        ProductColor.White,
        ProductColor.Gray,
        ProductColor.Multi,
    ];

    /// <summary>A color's place in <see cref="DisplayOrder"/>.</summary>
    /// <param name="color">The color.</param>
    /// <returns>Its zero-based position; an unknown value sorts last.</returns>
    public static int Position(ProductColor color)
    {
        for (var i = 0; i < DisplayOrder.Count; i++)
        {
            if (DisplayOrder[i] == color)
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}
