using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Settlements;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IDeferredPaymentQueries"/> over a list of rows.</summary>
internal sealed class FakeDeferredPaymentQueries : IDeferredPaymentQueries
{
    public List<DeferredHeld> Held { get; } = [];

    public DeferredSyncState Sync { get; set; } = new(false, false, null);

    /// <summary>Rows by marketplace id.</summary>
    public List<(string MarketplaceId, DeferredPaymentRow Row)> Rows { get; } = [];

    public List<DeferredPaymentRow> ReleasedInSettlement { get; } = [];

    public List<SettlementDeferral> BySettlement { get; } = [];

    public List<SkuDeferral> BySku { get; } = [];

    public List<DeferredLineItem> SkuLines { get; } = [];

    public List<string>? MarketplacesAsked { get; private set; }

    public (string MarketplaceId, string ListMarketplaceId, int MaxRows)? GroupsAsked { get; private set; }

    public Task<IReadOnlyList<DeferredHeld>> GetHeldAsync(string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeferredHeld>>(Held);

    public Task<DeferredSyncState> GetSyncStateAsync(string marketplaceId, CancellationToken cancellationToken) => Task.FromResult(Sync);

    public Task<PagedResult<DeferredPaymentRow>> GetPageAsync(string marketplaceId, PageRequest request, DeferredListFilter filter, CancellationToken cancellationToken)
    {
        var rows = Rows.Where(r => r.MarketplaceId == marketplaceId).Select(r => r.Row).ToList();
        return Task.FromResult(new PagedResult<DeferredPaymentRow>(rows, rows.Count, 1, PageRequest.DefaultPageSize));
    }

    public Task<IReadOnlyList<DeferredPaymentRow>> GetReleasedInSettlementAsync(string listMarketplaceId, string settlementId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeferredPaymentRow>>(ReleasedInSettlement);

    public Task<IReadOnlyList<DeferredPaymentRow>> GetByOrdersAsync(IReadOnlyCollection<string> marketplaceIds, IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeferredPaymentRow>>(
            [.. Rows.Where(r => marketplaceIds.Contains(r.MarketplaceId) && r.Row.AmazonOrderId is { } id && orderIds.Contains(id)).Select(r => r.Row)]);

    public Task<PagedResult<DeferredSettlementGroup>> GetGroupsAsync(
        string marketplaceId, string listMarketplaceId, PageRequest request, DeferredView view, int maxRowsPerGroup, CancellationToken cancellationToken)
    {
        GroupsAsked = (marketplaceId, listMarketplaceId, maxRowsPerGroup);
        return Task.FromResult(new PagedResult<DeferredSettlementGroup>([], 0, 1, request.SafePageSize));
    }

    public Task<IReadOnlyList<SettlementDeferral>> GetBySettlementsAsync(
        IReadOnlyCollection<string> marketplaceIds, IReadOnlyCollection<string> settlementIds, CancellationToken cancellationToken)
    {
        MarketplacesAsked = [.. marketplaceIds];
        return Task.FromResult<IReadOnlyList<SettlementDeferral>>([.. BySettlement.Where(d => settlementIds.Contains(d.SettlementId))]);
    }

    public Task<IReadOnlyList<SkuDeferral>> GetSkuDeferralsAsync(IReadOnlyCollection<string> marketplaceIds, string settlementId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SkuDeferral>>(BySku);

    public Task<IReadOnlyList<DeferredLineItem>> GetSkuLinesAsync(
        IReadOnlyCollection<string> marketplaceIds, string settlementId, string? sku, decimal? priceEach, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeferredLineItem>>(SkuLines);
}
