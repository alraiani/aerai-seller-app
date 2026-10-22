using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;

namespace AERai.Web.Application.Settlements;

/// <summary>
/// Payouts Amazon deferred: what it still holds for a marketplace (the Settlements tile and the Deferred
/// page) and which settled orders were held before they were paid (the Summary banner and Lines markers).
/// </summary>
public interface IDeferredPaymentService
{
    /// <summary>What Amazon holds for the marketplace now.</summary>
    /// <param name="marketplace">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The tile's numbers and how fresh they are.</returns>
    Task<DeferredOverview> GetOverviewAsync(Marketplace marketplace, CancellationToken cancellationToken);

    /// <summary>A page of the marketplace's deferred transactions.</summary>
    /// <param name="marketplace">Marketplace.</param>
    /// <param name="request">Page and search.</param>
    /// <param name="filter">View and sort.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page.</returns>
    Task<PagedResult<DeferredPaymentRow>> GetPageAsync(Marketplace marketplace, PageRequest request, DeferredListFilter filter, CancellationToken cancellationToken);

    /// <summary>The orders in a settlement that Amazon deferred before paying them out in it.</summary>
    /// <param name="marketplace">The marketplace whose settlements list holds the settlement.</param>
    /// <param name="settlementId">Settlement.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Their count and amounts.</returns>
    Task<SettlementDeferrals> GetSettlementDeferralsAsync(Marketplace marketplace, string settlementId, CancellationToken cancellationToken);

    /// <summary>
    /// Each order's deferral, for orders Amazon deferred: the held transaction while any is held,
    /// otherwise the latest released one.
    /// </summary>
    /// <param name="marketplace">The marketplace whose orders or settlements are shown.</param>
    /// <param name="orderIds">Amazon order ids.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Order id → its deferral; orders never deferred are absent.</returns>
    Task<IReadOnlyDictionary<string, DeferredPaymentRow>> GetOrderDeferralsAsync(Marketplace marketplace, IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken);

    /// <summary>
    /// A page of the marketplace's deferred transactions grouped by the settlement they were sold in, each group
    /// with its first <see cref="DeferredPaymentService.MaxGroupRows"/> transactions.
    /// </summary>
    /// <param name="marketplace">Marketplace.</param>
    /// <param name="request">Page (of groups) and search.</param>
    /// <param name="view">Still held, released, or both.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page of groups.</returns>
    Task<PagedResult<DeferredSettlementGroup>> GetGroupsAsync(Marketplace marketplace, PageRequest request, DeferredView view, CancellationToken cancellationToken);

    /// <summary>
    /// What Amazon deferred out of each settlement's payout (sales made in its period that it held back), in the
    /// currency the page shows: converted at the settlement's rate when <paramref name="amounts"/> is the
    /// marketplace's currency and the settlement has a rate, otherwise as paid.
    /// </summary>
    /// <param name="marketplace">The marketplace whose settlements list holds the settlements.</param>
    /// <param name="settlements">The settlements shown.</param>
    /// <param name="amounts">Which currency the page shows.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Settlement id → its deferrals; settlements with none are absent.</returns>
    Task<IReadOnlyDictionary<string, DeferredOut>> GetDeferredOutAsync(
        Marketplace marketplace, IReadOnlyCollection<SettlementSummary> settlements, SettlementAmounts amounts, CancellationToken cancellationToken);

    /// <summary>
    /// What Amazon deferred out of a settlement per SKU and price each, amounts in the currency the page shows.
    /// <see cref="SkuDeferral.PriceEach"/> stays as paid, so it matches the summary rows' own price each.
    /// </summary>
    /// <param name="marketplace">The marketplace whose settlements list holds the settlement.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="amounts">Which currency the page shows.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One row per SKU and price each.</returns>
    Task<IReadOnlyList<SkuDeferral>> GetSkuDeferralsAsync(
        Marketplace marketplace, SettlementSummary settlement, SettlementAmounts amounts, CancellationToken cancellationToken);

    /// <summary>The deferred products of one summary row (SKU and price each), amounts in the currency the page shows.</summary>
    /// <param name="marketplace">The marketplace whose settlements list holds the settlement.</param>
    /// <param name="settlement">The settlement.</param>
    /// <param name="sku">SKU, or <see langword="null"/> for the row without one.</param>
    /// <param name="priceEach">Price each as paid, or <see langword="null"/>.</param>
    /// <param name="amounts">Which currency the page shows.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The products, soonest release first.</returns>
    Task<IReadOnlyList<DeferredLineItem>> GetSkuLinesAsync(
        Marketplace marketplace, SettlementSummary settlement, string? sku, decimal? priceEach, SettlementAmounts amounts, CancellationToken cancellationToken);
}
