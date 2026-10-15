namespace AERai.Web.Application.Products;

/// <summary>Order of products on the Products list.</summary>
public enum ProductSort
{
    /// <summary>SKU, A to Z.</summary>
    Sku = 0,

    /// <summary>Title, A to Z (products without a title last).</summary>
    Title = 1,

    /// <summary>Family, A to Z, then SKU (products without a family last).</summary>
    Family = 2,

    /// <summary>Highest cost first (products without a cost last).</summary>
    Cost = 3,

    /// <summary>Most recently changed first.</summary>
    Updated = 4,
}
