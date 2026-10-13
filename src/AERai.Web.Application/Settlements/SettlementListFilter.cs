namespace AERai.Web.Application.Settlements;

/// <summary>Which settlements the Settlements list shows, and in what order (search and paging are in the page request).</summary>
/// <param name="PeriodEndFrom">Only settlements whose period ends at or after this instant, or <see langword="null"/>.</param>
/// <param name="PeriodEndBefore">Only settlements whose period ends before this instant, or <see langword="null"/>.</param>
/// <param name="Currency">Only settlements paid in this ISO 4217 currency, or <see langword="null"/> for all.</param>
/// <param name="Sort">Order.</param>
/// <param name="Descending">Reverse the sort's natural direction (see <see cref="SettlementSorts"/>).</param>
public sealed record SettlementListFilter(
    DateTimeOffset? PeriodEndFrom = null,
    DateTimeOffset? PeriodEndBefore = null,
    string? Currency = null,
    SettlementSort Sort = SettlementSort.PeriodEnd,
    bool Descending = false)
{
    /// <summary>Every settlement, newest first.</summary>
    public static SettlementListFilter Default { get; } = new();
}
