namespace AERai.Web.Application.Orders;

/// <summary>Which orders the Orders list shows, and in what order (search and paging are in the page request).</summary>
/// <param name="PurchasedFrom">Only orders placed at or after this instant, or <see langword="null"/>.</param>
/// <param name="PurchasedBefore">Only orders placed before this instant, or <see langword="null"/>.</param>
/// <param name="Status">Only orders with this status, or <see langword="null"/> for all.</param>
/// <param name="Sort">Order.</param>
/// <param name="Descending">Reverse the sort's natural direction (see <see cref="OrderSorts"/>).</param>
public sealed record OrderListFilter(
    DateTimeOffset? PurchasedFrom = null,
    DateTimeOffset? PurchasedBefore = null,
    string? Status = null,
    OrderSort Sort = OrderSort.Purchased,
    bool Descending = false)
{
    /// <summary>Every order, newest first.</summary>
    public static OrderListFilter Default { get; } = new();
}
