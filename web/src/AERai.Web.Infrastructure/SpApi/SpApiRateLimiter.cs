using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// One token bucket per SP-API operation, shared by every request in the process (singleton), so a
/// burst of one operation can never exhaust another's quota.
/// </summary>
internal sealed class SpApiRateLimiter : IDisposable
{
    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _buckets = new(StringComparer.Ordinal);

    /// <summary>Waits for permission to call an operation.</summary>
    /// <param name="operation">Operation name from <see cref="SpApiOperation"/>.</param>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>A lease that must be disposed after the call.</returns>
    public ValueTask<RateLimitLease> AcquireAsync(string operation, CancellationToken cancellationToken) =>
        _buckets.GetOrAdd(operation, Create).AcquireAsync(1, cancellationToken);

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var bucket in _buckets.Values)
        {
            bucket.Dispose();
        }
    }

    private static TokenBucketRateLimiter Create(string operation)
    {
        var (rate, burst) = SpApiOperation.Limits.GetValueOrDefault(operation, SpApiOperation.DefaultLimit);
        return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = burst,
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1 / rate),
            QueueLimit = 1_000,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }
}
