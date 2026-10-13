namespace AERai.Web.Application.Settlements;

/// <summary>Which rows of a settlement's per-SKU summary to list, and in what order.</summary>
/// <param name="Kind">Which rows.</param>
/// <param name="Sort">Order.</param>
/// <param name="Descending">Reverse the sort's natural direction (see <see cref="SettlementSorts"/>).</param>
public sealed record SettlementSkuFilter(
    SettlementRowKind Kind = SettlementRowKind.All,
    SettlementSkuSort Sort = SettlementSkuSort.Sales,
    bool Descending = false)
{
    /// <summary>Every row, largest sales first.</summary>
    public static SettlementSkuFilter Default { get; } = new();
}
