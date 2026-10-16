namespace AERai.Web.Application.Orders;

/// <summary>Order of orders on the Orders list.</summary>
public enum OrderSort
{
    /// <summary>Newest purchase first.</summary>
    Purchased = 0,

    /// <summary>Most units first.</summary>
    Units = 1,

    /// <summary>Largest sales first.</summary>
    Sales = 2,
}
