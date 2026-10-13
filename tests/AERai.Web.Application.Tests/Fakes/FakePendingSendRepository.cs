using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IPendingSendRepository"/> seeded by each test.</summary>
internal sealed class FakePendingSendRepository : IPendingSendRepository
{
    public List<PendingSend> Sends { get; } = [];

    /// <summary>Who closed each send, by id.</summary>
    public Dictionary<long, string> ClosedBy { get; } = [];

    public Task<IReadOnlyList<PendingSend>> ListOpenAsync(string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PendingSend>>(Sends.Where(s => s.IsOpen).OrderBy(s => s.CreatedAt).ToList());

    public Task<IReadOnlyList<PendingSend>> ListSinceAsync(string marketplaceId, IReadOnlyCollection<string> skus, DateTimeOffset since, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PendingSend>>(Sends.Where(s => skus.Contains(s.Sku) && s.CreatedAt >= since).OrderBy(s => s.CreatedAt).ToList());

    public Task<PendingSend?> GetAsync(string marketplaceId, long id, CancellationToken cancellationToken) =>
        Task.FromResult(Sends.SingleOrDefault(s => s.Id == id));

    public Task<int> ReconcileAsync(string marketplaceId, IReadOnlyList<PendingSendMatch> matches, DateTimeOffset at, string by, CancellationToken cancellationToken)
    {
        var updated = 0;
        foreach (var match in matches)
        {
            var index = Sends.FindIndex(s => s.Id == match.Id && s.IsOpen);
            if (index < 0)
            {
                continue;
            }

            Sends[index] = Sends[index] with { ReconciledUnits = match.ReconciledUnits, IsOpen = !match.Closed };
            if (match.Closed)
            {
                ClosedBy[match.Id] = by;
            }

            updated++;
        }

        return Task.FromResult(updated);
    }

    public Task<bool> SetReferenceAsync(string marketplaceId, long id, string reference, CancellationToken cancellationToken)
    {
        var index = Sends.FindIndex(s => s.Id == id && s.IsOpen && s.Reference is null);
        if (index >= 0)
        {
            Sends[index] = Sends[index] with { Reference = reference };
        }

        return Task.FromResult(index >= 0);
    }
}
