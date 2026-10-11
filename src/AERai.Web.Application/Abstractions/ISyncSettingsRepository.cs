using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Persistence for the single <see cref="SyncSettings"/> row.
/// </summary>
public interface ISyncSettingsRepository
{
    /// <summary>Gets the current settings.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The settings (defaults when the row is missing).</returns>
    Task<SyncSettings> GetAsync(CancellationToken cancellationToken);

    /// <summary>Pauses or resumes all scheduled runs.</summary>
    /// <param name="paused">Desired state.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="at">When the change happened.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SetPausedAsync(bool paused, string user, DateTimeOffset at, CancellationToken cancellationToken);
}
