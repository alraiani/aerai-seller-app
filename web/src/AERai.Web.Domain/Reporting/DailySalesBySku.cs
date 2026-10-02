namespace AERai.Web.Domain.Reporting;

/// <summary>
/// Read model for <c>rpt.vw_DailySalesBySku</c>: units and revenue per SKU per UTC purchase day,
/// excluding cancelled orders.
/// </summary>
public sealed class DailySalesBySku
{
    /// <summary>UTC calendar date of the purchase.</summary>
    public DateOnly SalesDate { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>Product title, when known.</summary>
    public string? Title { get; set; }

    /// <summary>Distinct orders containing the SKU that day.</summary>
    public int OrderCount { get; set; }

    /// <summary>Units sold.</summary>
    public int Units { get; set; }

    /// <summary>Sum of item prices.</summary>
    public decimal Revenue { get; set; }
}
