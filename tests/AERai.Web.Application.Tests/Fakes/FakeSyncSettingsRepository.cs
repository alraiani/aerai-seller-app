using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="ISyncSettingsRepository"/>.</summary>
internal sealed class FakeSyncSettingsRepository : ISyncSettingsRepository
{
    public SyncSettings Settings { get; } = new();

    public Task<SyncSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Settings);

    public Task SetPausedAsync(bool paused, string user, DateTimeOffset at, CancellationToken cancellationToken)
    {
        Settings.IsPaused = paused;
        Settings.PausedAt = paused ? at : null;
        Settings.PausedBy = paused ? user : null;
        return Task.CompletedTask;
    }
}
