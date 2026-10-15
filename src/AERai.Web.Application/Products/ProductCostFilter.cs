namespace AERai.Web.Application.Products;

/// <summary>Which products the Products list shows by whether their cost of goods is set.</summary>
public enum ProductCostFilter
{
    /// <summary>Every product.</summary>
    All = 0,

    /// <summary>Products with no cost in the marketplace (they make margins unknown).</summary>
    Missing = 1,

    /// <summary>Products with a cost in the marketplace.</summary>
    Set = 2,
}
