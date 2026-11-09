using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IFinancialTransactionRepository"/> keyed by (marketplace, transaction id).</summary>
internal sealed class FakeFinancialTransactionRepository : IFinancialTransactionRepository
{
    public Dictionary<(string MarketplaceId, string TransactionId), AmazonFinancialTransaction> Stored { get; } = [];

    /// <summary>How many upsert calls were made (one per page with transactions).</summary>
    public int Calls { get; private set; }

    public Task<(int Inserted, int Updated)> UpsertAsync(
        string marketplaceId, IReadOnlyCollection<AmazonFinancialTransaction> transactions, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        Calls++;
        var inserted = 0;
        foreach (var t in transactions)
        {
            inserted += Stored.ContainsKey((marketplaceId, t.TransactionId)) ? 0 : 1;
            Stored[(marketplaceId, t.TransactionId)] = t;
        }

        return Task.FromResult((inserted, transactions.Count - inserted));
    }

    /// <summary>The held ids each <see cref="MarkReleasedAsync"/> call was given.</summary>
    public List<IReadOnlyCollection<string>> StillHeldCalls { get; } = [];

    public Task<int> MarkReleasedAsync(string marketplaceId, IReadOnlyCollection<string> stillHeld, CancellationToken cancellationToken)
    {
        StillHeldCalls.Add(stillHeld);
        return Task.FromResult(0);
    }
}
