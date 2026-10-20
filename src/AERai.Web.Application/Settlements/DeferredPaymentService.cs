using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Settlements;

/// <summary>Default <see cref="IDeferredPaymentService"/>.</summary>
/// <param name="queries">Deferred transaction rows.</param>
/// <param name="marketplaces">Marketplace lookups (which marketplaces a settlements list holds).</param>
/// <param name="time">Clock (which release dates have passed).</param>
public sealed class DeferredPaymentService(IDeferredPaymentQueries queries, IMarketplaceQueries marketplaces, TimeProvider time) : IDeferredPaymentService
{
    /// <summary>
    /// Transactions shown in each settlement group on the Deferred page; a group with more links to its own
    /// full list.
    /// </summary>
    public const int MaxGroupRows = 250;

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
        return queries.GetPageAsync(marketplace.MarketplaceId, request, filter, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PagedResult<DeferredSettlementGroup>> GetGroupsAsync(Marketplace marketplace, PageRequest request, DeferredView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(request);
        return queries.GetGroupsAsync(
            marketplace.MarketplaceId, marketplace.SettlementMarketplaceId ?? marketplace.MarketplaceId, request, view, MaxGroupRows, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, DeferredOut>> GetDeferredOutAsync(
        Marketplace marketplace, IReadOnlyCollection<SettlementSummary> settlements, SettlementAmounts amounts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(settlements);
        var result = new Dictionary<string, DeferredOut>(StringComparer.Ordinal);
        if (settlements.Count == 0)
        {
            return result;
        }

        var listed = await ListedMarketplacesAsync(marketplace, cancellationToken).ConfigureAwait(false);
        var rows = (await queries.GetBySettlementsAsync(listed, [.. settlements.Select(s => s.SettlementId)], cancellationToken).ConfigureAwait(false))
            .ToLookup(r => r.SettlementId, StringComparer.Ordinal);
        foreach (var settlement in settlements)
        {
            // A settlement's deferrals are in its own currency; should Amazon ever mix in another, the settlement's
            // own one is shown rather than adding currencies together.
            var deferrals = rows[settlement.SettlementId].ToList();
            var row = deferrals.FirstOrDefault(r => string.Equals(r.Currency, settlement.Currency, StringComparison.OrdinalIgnoreCase)) ?? deferrals.FirstOrDefault();
            if (row is null)
            {
                continue;
            }

            var (amount, currency) = Shown(row.Amount, row.Currency, settlement, amounts);
            var (held, _) = Shown(row.HeldAmount, row.Currency, settlement, amounts);
            result[settlement.SettlementId] = new DeferredOut(amount, held, currency, row.Count, row.Orders, row.HeldCount, row.FirstRelease, row.LastRelease);
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SkuDeferral>> GetSkuDeferralsAsync(
        Marketplace marketplace, SettlementSummary settlement, SettlementAmounts amounts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(settlement);

        var listed = await ListedMarketplacesAsync(marketplace, cancellationToken).ConfigureAwait(false);
        var rows = await queries.GetSkuDeferralsAsync(listed, settlement.SettlementId, cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(r =>
        {
            var (amount, currency) = Shown(r.Amount, r.Currency, settlement, amounts);
            return r with { Amount = amount, Currency = currency };
        })];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeferredLineItem>> GetSkuLinesAsync(
        Marketplace marketplace, SettlementSummary settlement, string? sku, decimal? priceEach, SettlementAmounts amounts, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(settlement);

        var listed = await ListedMarketplacesAsync(marketplace, cancellationToken).ConfigureAwait(false);
        var rows = await queries.GetSkuLinesAsync(listed, settlement.SettlementId, sku, priceEach, cancellationToken).ConfigureAwait(false);
        return [.. rows.Select(r =>
        {
            var (amount, currency) = Shown(r.Amount, r.Currency, settlement, amounts);
            return r with { Amount = amount, Currency = currency };
        })];
    }

    /// <summary>
    /// An amount in the currency the settlement page shows. In the marketplace's currency it is converted at the
    /// settlement's own rate (the rule <c>rpt.vw_SettlementRate</c> applied to the settlement) and rounded like the
    /// views round; without a rate, or as paid, it stays as it is.
    /// </summary>
    private static (decimal Amount, string Currency) Shown(decimal amount, string currency, SettlementSummary settlement, SettlementAmounts amounts) =>
        amounts == SettlementAmounts.Home
            && settlement.ExchangeRate is { } rate
            && string.Equals(currency, settlement.Currency, StringComparison.OrdinalIgnoreCase)
            ? (decimal.Round(amount * rate, 2, MidpointRounding.AwayFromZero), settlement.HomeCurrency)
            : (amount, currency);

    /// <summary>The marketplaces whose payouts a marketplace's settlements list holds: itself and any listed under it.</summary>
    private async Task<List<string>> ListedMarketplacesAsync(Marketplace marketplace, CancellationToken cancellationToken)
    {
        var all = await marketplaces.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return [.. all
            .Where(m => m.MarketplaceId == marketplace.MarketplaceId || m.SettlementMarketplaceId == marketplace.MarketplaceId)
            .Select(m => m.MarketplaceId)];
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
        var listed = await ListedMarketplacesAsync(marketplace, cancellationToken).ConfigureAwait(false);
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
