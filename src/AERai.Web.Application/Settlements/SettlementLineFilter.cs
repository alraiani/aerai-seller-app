namespace AERai.Web.Application.Settlements;

/// <summary>Which order groups behind a per-SKU summary row to list, and in what order.</summary>
/// <param name="Bucket">Only groups with a non-zero amount in this column, or <see langword="null"/> for all.</param>
/// <param name="Sort">Order.</param>
/// <param name="Descending">Reverse the sort's natural direction (see <see cref="SettlementSorts"/>).</param>
public sealed record SettlementLineFilter(
    SettlementBucket? Bucket = null,
    SettlementGroupSort Sort = SettlementGroupSort.Posted,
    bool Descending = false)
{
    /// <summary>Every group, earliest posted first.</summary>
    public static SettlementLineFilter Default { get; } = new();
}
