using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace AERai.Seller.SpApiClient;

/// <summary>Requests-per-second + burst for one SP-API operation. Rate limits are per-operation, not global.</summary>
public sealed record SpApiRateLimit(double RequestsPerSecond, int Burst);

/// <summary>
/// Token-bucket rate limiter keyed by operation name, so one hot operation can't starve others.
/// Callers that don't register an explicit limit fall back to a conservative default.
/// </summary>
public sealed class SpApiOperationRateLimiter(IReadOnlyDictionary<string, SpApiRateLimit>? rateLimitsByOperation = null)
    : IDisposable
{
    private static readonly SpApiRateLimit DefaultRateLimit = new(RequestsPerSecond: 1, Burst: 2);

    private readonly IReadOnlyDictionary<string, SpApiRateLimit> _rateLimitsByOperation =
        rateLimitsByOperation ?? new Dictionary<string, SpApiRateLimit>();

    private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new();

    public async Task WaitAsync(string operationName, CancellationToken cancellationToken)
    {
        var limiter = _limiters.GetOrAdd(operationName, CreateLimiter);
        using var lease = await limiter.AcquireAsync(1, cancellationToken);
        if (!lease.IsAcquired)
        {
            throw new InvalidOperationException($"Could not acquire a rate-limit lease for SP-API operation '{operationName}'.");
        }
    }

    private TokenBucketRateLimiter CreateLimiter(string operationName)
    {
        var rateLimit = _rateLimitsByOperation.GetValueOrDefault(operationName, DefaultRateLimit);
        return new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = Math.Max(1, rateLimit.Burst),
            TokensPerPeriod = 1,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1.0 / rateLimit.RequestsPerSecond),
            QueueLimit = int.MaxValue,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true,
        });
    }

    public void Dispose()
    {
        foreach (var limiter in _limiters.Values)
        {
            limiter.Dispose();
        }
    }
}
