using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Settlements;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Settlements;

/// <summary>
/// Payouts Amazon deferred for the current marketplace, grouped by the settlement the sales were made in: the
/// open settlement first, then newest period first. Each group expands into its transactions. With
/// <c>?settlement=</c> it lists one settlement's transactions in full, sortable. Views: still held, released, or both.
/// </summary>
/// <param name="deferred">Deferred payouts.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class DeferredModel(IDeferredPaymentService deferred, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>Settlement groups per page.</summary>
    public const int GroupsPerPage = 10;

    /// <summary>Still held, released, or both, from <c>?view=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "view")]
    public DeferredView View { get; set; }

    /// <summary>One settlement's transactions only (or <c>none</c> for those without one), from <c>?settlement=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "settlement")]
    public string? Settlement { get; set; }

    /// <summary>Sort for one settlement's list, from <c>?sort=</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "sort")]
    public DeferredSort Sort { get; set; }

    /// <summary>Reverse the sort, from <c>?desc=true</c>.</summary>
    [BindProperty(SupportsGet = true, Name = "desc")]
    public bool Descending { get; set; }

    /// <summary>The marketplace shown.</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>The marketplace's time zone, in which dates are shown.</summary>
    public TimeZoneInfo Zone { get; private set; } = TimeZoneInfo.Utc;

    /// <summary>What Amazon holds now (the tile at the top).</summary>
    public DeferredOverview Overview { get; private set; } = new([], new DeferredSyncState(false, false, null), DateTimeOffset.MinValue);

    /// <summary>The page of settlement groups (when no single settlement is chosen).</summary>
    public PagedResult<DeferredSettlementGroup>? Groups { get; private set; }

    /// <summary>One settlement's transactions (when <see cref="Settlement"/> is set).</summary>
    public PagedResult<DeferredPaymentRow>? Rows { get; private set; }

    /// <summary>Whether one settlement is shown in full.</summary>
    public bool IsSingleSettlement => !string.IsNullOrWhiteSpace(Settlement);

    /// <summary>Loads the overview and the requested groups or settlement.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or just its section for a partial reload.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Zone = TimeZoneInfo.FindSystemTimeZoneById(Marketplace.TimeZoneId);
        Overview = await deferred.GetOverviewAsync(Marketplace, cancellationToken);
        if (IsSingleSettlement)
        {
            Rows = await deferred.GetPageAsync(Marketplace, ToPageRequest(), new DeferredListFilter(View, Sort, Descending, Settlement!.Trim()), cancellationToken); // Non-null: IsSingleSettlement.
        }
        else
        {
            Groups = await deferred.GetGroupsAsync(Marketplace, new PageRequest(PageNumber, GroupsPerPage, Search), View, cancellationToken);
        }

        return this.PageOrFragment("_DeferredBody");
    }

    /// <summary>Link to a view, keeping the search and settlement.</summary>
    /// <param name="view">View.</param>
    /// <returns>A relative URL.</returns>
    public string ViewLink(DeferredView view) => LinkWith(("view", view == DeferredView.Deferred ? null : view.ToString()), ("p", null));

    /// <summary>Link to one settlement's full list, keeping the view.</summary>
    /// <param name="settlementId">Settlement, or <see langword="null"/> for transactions without one.</param>
    /// <returns>A relative URL.</returns>
    public string SettlementLink(string? settlementId) =>
        LinkWith(("settlement", settlementId ?? DeferredListFilter.NoSettlement), ("p", null), ("q", null));

    /// <summary>Link back to every settlement, keeping the view.</summary>
    /// <returns>A relative URL.</returns>
    public string AllSettlementsLink() => LinkWith(("settlement", null), ("sort", null), ("desc", null), ("p", null));

    /// <summary>Link for a sortable column header: toggles direction when it's already the sort.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>A relative URL.</returns>
    public string SortLink(DeferredSort column) =>
        LinkWith(("sort", column == DeferredSort.Maturity ? null : column.ToString()), ("desc", Sort == column && !Descending ? "true" : null));

    /// <summary>Arrow for a sortable column header.</summary>
    /// <param name="column">Sort.</param>
    /// <returns>"↑", "↓", or empty.</returns>
    public string SortArrow(DeferredSort column) =>
        SettlementFormat.Arrow(Sort == column, (column != DeferredSort.Maturity) != Descending);

    /// <summary>A group's heading: its settlement and period, or what it is when there is none.</summary>
    /// <param name="group">The group.</param>
    /// <returns>e.g. "Oct 4 – Oct 18, 2026", "Not paid out yet · the settlement still open", or "Not imported here · sold Jul 13 – Jul 27, 2026".</returns>
    public string GroupPeriod(DeferredSettlementGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        return group switch
        {
            { SettlementId: null } => "Amazon gave no settlement",
            { IsImported: true } => SettlementFormat.Period(group.PeriodStart, group.PeriodEnd, Zone),
            { IsOpen: true } => "Not paid out yet · the settlement still open",
            _ => $"Not imported here · sold {SettlementFormat.Period(group.FirstPosted, group.LastPosted, Zone)}",
        };
    }
}
