namespace AERai.Web.Domain.Core;

/// <summary>
/// The color of a product variant, picked from a fixed palette so each one can be shown in a
/// theme-aware color (light and dark) on the inventory worksheet. Declaration order is the order
/// color groups appear in.
/// </summary>
public enum ProductColor
{
    /// <summary>Black.</summary>
    Black = 1,

    /// <summary>White.</summary>
    White = 2,

    /// <summary>Gray.</summary>
    Gray = 3,

    /// <summary>Red.</summary>
    Red = 4,

    /// <summary>Orange.</summary>
    Orange = 5,

    /// <summary>Yellow.</summary>
    Yellow = 6,

    /// <summary>Green.</summary>
    Green = 7,

    /// <summary>Teal.</summary>
    Teal = 8,

    /// <summary>Blue.</summary>
    Blue = 9,

    /// <summary>Navy.</summary>
    Navy = 10,

    /// <summary>Purple.</summary>
    Purple = 11,

    /// <summary>Pink.</summary>
    Pink = 12,

    /// <summary>Brown.</summary>
    Brown = 13,

    /// <summary>Beige.</summary>
    Beige = 14,

    /// <summary>Several colors in one product (e.g. a mixed pack).</summary>
    Multi = 15,
}
