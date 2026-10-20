using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

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
}
