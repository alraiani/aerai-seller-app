using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Alerts;
using AERai.Web.Domain.Alerts;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IStockAlertRepository"/>.</summary>
internal sealed class FakeStockAlertRepository : IStockAlertRepository
{
    private long _nextId = 1;

    public List<StockAlert> Alerts { get; } = [];

    public HashSet<(long AlertId, string User)> Reads { get; } = [];

    public IReadOnlyList<StockAlert> Open => Alerts.Where(a => a.ResolvedAt is null).ToList();

    public Task<IReadOnlyList<StockAlert>> GetOpenAsync(string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockAlert>>(Alerts.Where(a => a.MarketplaceId == marketplaceId && a.ResolvedAt is null)
            .Select(a => new StockAlert { Id = a.Id, MarketplaceId = a.MarketplaceId, Sku = a.Sku, Level = a.Level, Message = a.Message, RaisedAt = a.RaisedAt, UpdatedAt = a.UpdatedAt })
            .ToList());

    public Task<bool> ApplyAsync(StockAlertChanges changes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        foreach (var alert in Alerts.Where(a => changes.Resolve.Contains(a.Id)))
        {
            alert.ResolvedAt = now;
        }

        foreach (var (id, message) in changes.Messages)
        {
            Alerts.Single(a => a.Id == id).Message = message;
        }

        foreach (var alert in changes.Raise)
        {
            alert.Id = _nextId++;
            Alerts.Add(alert);
        }

        return Task.FromResult(true);
    }

    public Task<int> CountUnreadAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken) =>
        Task.FromResult(Alerts.Count(a => a.MarketplaceId == marketplaceId && a.ResolvedAt is null && !Reads.Contains((a.Id, userEmail))));

    public Task<IReadOnlyList<StockAlertView>> ListAsync(string marketplaceId, string userEmail, DateTimeOffset resolvedSince, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<StockAlertView>>(Alerts
            .Where(a => a.MarketplaceId == marketplaceId && (a.ResolvedAt is null || a.ResolvedAt >= resolvedSince))
            .Select(a => new StockAlertView(a.Id, a.Sku, null, a.Level, a.Message, a.RaisedAt, a.ResolvedAt, Reads.Contains((a.Id, userEmail))))
            .ToList());

    public Task<int> MarkReadAsync(string marketplaceId, IReadOnlyCollection<long>? alertIds, string userEmail, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var ids = Alerts
            .Where(a => a.MarketplaceId == marketplaceId && (alertIds is null ? a.ResolvedAt is null : alertIds.Contains(a.Id)))
            .Select(a => a.Id)
            .Where(id => Reads.Add((id, userEmail)))
            .ToList();
        return Task.FromResult(ids.Count);
    }
}
