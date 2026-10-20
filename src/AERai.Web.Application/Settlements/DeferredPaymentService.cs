using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Settlements;

/// <summary>Default <see cref="IDeferredPaymentService"/>.</summary>
/// <param name="queries">Deferred transaction rows.</param>
/// <param name="marketplaces">Marketplace lookups (which marketplaces a settlements list holds).</param>
/// <param name="time">Clock (the released view's window).</param>
public sealed class DeferredPaymentService(IDeferredPaymentQueries queries, IMarketplaceQueries marketplaces, TimeProvider time) : IDeferredPaymentService
{
    /// <summary>How far back the Deferred page's released view reaches.</summary>
    public static readonly TimeSpan ReleasedWindow = TimeSpan.FromDays(30);

    /// <inheritdoc/>
    public async Task<DeferredOverview> GetOverviewAsync(Marketplace marketplace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var held = await queries.GetHeldAsync(marketplace.MarketplaceId, cancellationToken).ConfigureAwait(false);
        var sync = await queries.GetSyncStateAsync(marketplace.MarketplaceId, cancellationToken).ConfigureAwait(false);

        // Amounts in different currencies are never added together; the marketplace's own comes first.
        var ordered = held
            .OrderBy(h => string.Equals(h.Currency, marketplace.Currency, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(h => h.Currency, StringComparer.Ordinal)
            .ToList();
        return new DeferredOverview(ordered, sync, time.GetUtcNow());
    }

    /// <inheritdoc/>
    public Task<PagedResult<DeferredPaymentRow>> GetPageAsync(Marketplace marketplace, PageRequest request, DeferredListFilter filter, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(filter);
        return queries.GetPageAsync(marketplace.MarketplaceId, request, filter, time.GetUtcNow() - ReleasedWindow, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<SettlementDeferrals> GetSettlementDeferralsAsync(Marketplace marketplace, string settlementId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);

        var rows = await queries.GetReleasedInSettlementAsync(marketplace.MarketplaceId, settlementId, cancellationToken).ConfigureAwait(false);
        if (rows.Count == 0)
        {
            return SettlementDeferrals.None;
        }

        var orders = rows.Where(r => r.AmazonOrderId is not null).Select(r => r.AmazonOrderId).Distinct(StringComparer.Ordinal).Count();
        var amounts = rows
            .GroupBy(r => r.Currency ?? marketplace.Currency, StringComparer.OrdinalIgnoreCase)
            .Select(g => (g.Key, g.Sum(r => r.Amount)))
            .OrderBy(a => string.Equals(a.Key, marketplace.Currency, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ToList();
        return new SettlementDeferrals(orders, amounts);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, DeferredPaymentRow>> GetOrderDeferralsAsync(Marketplace marketplace, IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(orderIds);
        if (orderIds.Count == 0)
        {
            return new Dictionary<string, DeferredPaymentRow>(StringComparer.Ordinal);
        }

        // A settlements list can hold another marketplace's payouts (Canada's are in the United States
        // list), so the orders shown may belong to any marketplace listed under this one.
        var all = await marketplaces.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var listed = all
            .Where(m => m.MarketplaceId == marketplace.MarketplaceId || m.SettlementMarketplaceId == marketplace.MarketplaceId)
            .Select(m => m.MarketplaceId)
            .ToList();
        var rows = await queries.GetByOrdersAsync(listed, orderIds, cancellationToken).ConfigureAwait(false);

        // An order can have several transactions (a shipment split, an adjustment). While any is held the
        // order counts as held; otherwise the latest release describes it.
        return rows
            .Where(r => r.AmazonOrderId is not null)
            .GroupBy(r => r.AmazonOrderId!, StringComparer.Ordinal) // Non-null: filtered just above.
            .ToDictionary(
                g => g.Key,
                g => g.FirstOrDefault(r => r.Status == DeferredTransactionStatus.Deferred)
                    ?? g.OrderByDescending(r => r.ReleasedAt ?? r.MaturityDate).First(),
                StringComparer.Ordinal);
    }
}
