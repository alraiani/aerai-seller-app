namespace AERai.Web.Application.Advertising;

/// <summary>How SKUs are ordered within each family on the Advertising page.</summary>
public enum AdSort
{
    /// <summary>Ad spend.</summary>
    Spend = 0,

    /// <summary>Sales attributed to the ads.</summary>
    AdSales = 1,

    /// <summary>Ad spend ÷ attributed sales.</summary>
    Acos = 2,

    /// <summary>Ad spend ÷ all sales of the SKU.</summary>
    Tacos = 3,

    /// <summary>Clicks.</summary>
    Clicks = 4,

    /// <summary>All sales of the SKU (ordered or not through an ad).</summary>
    TotalSales = 5,
}
