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

    public DateTimeOffset? ReleasedSinceSeen { get; private set; }

    public Task<IReadOnlyList<DeferredHeld>> GetHeldAsync(string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeferredHeld>>(Held);

    public Task<DeferredSyncState> GetSyncStateAsync(string marketplaceId, CancellationToken cancellationToken) => Task.FromResult(Sync);

    public Task<PagedResult<DeferredPaymentRow>> GetPageAsync(string marketplaceId, PageRequest request, DeferredListFilter filter, DateTimeOffset releasedSince, CancellationToken cancellationToken)
    {
        ReleasedSinceSeen = releasedSince;
        var rows = Rows.Where(r => r.MarketplaceId == marketplaceId).Select(r => r.Row).ToList();
        return Task.FromResult(new PagedResult<DeferredPaymentRow>(rows, rows.Count, 1, PageRequest.DefaultPageSize));
    }

    public Task<IReadOnlyList<DeferredPaymentRow>> GetReleasedInSettlementAsync(string listMarketplaceId, string settlementId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeferredPaymentRow>>(ReleasedInSettlement);

    public Task<IReadOnlyList<DeferredPaymentRow>> GetByOrdersAsync(IReadOnlyCollection<string> marketplaceIds, IReadOnlyCollection<string> orderIds, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DeferredPaymentRow>>(
            [.. Rows.Where(r => marketplaceIds.Contains(r.MarketplaceId) && r.Row.AmazonOrderId is { } id && orderIds.Contains(id)).Select(r => r.Row)]);
}
