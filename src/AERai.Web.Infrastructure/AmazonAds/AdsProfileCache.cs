using System.Collections.Concurrent;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>
/// Remembers each region's advertising profiles for a few hours, so a sync's report request and its later
/// status checks don't each list profiles again. Singleton, thread-safe.
/// </summary>
/// <param name="clock">Clock for expiry.</param>
internal sealed class AdsProfileCache(TimeProvider clock)
{
    /// <summary>Profiles rarely change; a newly authorized account is picked up within this time (or on restart).</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(6);

    private readonly ConcurrentDictionary<AmazonRegion, (IReadOnlyList<AdsProfile> Profiles, DateTimeOffset LoadedAt)> _profiles = new();

    /// <summary>The region's profiles, loading them when missing or stale.</summary>
    /// <param name="region">Region.</param>
    /// <param name="load">Lists the region's profiles from Amazon.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profiles.</returns>
    public async Task<IReadOnlyList<AdsProfile>> GetAsync(
        AmazonRegion region, Func<CancellationToken, Task<IReadOnlyList<AdsProfile>>> load, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(load);

        var now = clock.GetUtcNow();
        if (_profiles.TryGetValue(region, out var cached) && now - cached.LoadedAt < Lifetime)
        {
            return cached.Profiles;
        }

        // Two callers loading at once just both ask Amazon; the result is the same, so no lock is needed.
        var profiles = await load(cancellationToken).ConfigureAwait(false);
        _profiles[region] = (profiles, now);
        return profiles;
    }

    /// <summary>Forgets a region's profiles, so the next lookup asks Amazon again (after a profile is not found).</summary>
    /// <param name="region">Region.</param>
    public void Invalidate(AmazonRegion region) => _profiles.TryRemove(region, out _);
}
