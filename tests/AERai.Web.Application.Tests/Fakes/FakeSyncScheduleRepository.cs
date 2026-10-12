using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="ISyncScheduleRepository"/>.</summary>
internal sealed class FakeSyncScheduleRepository : ISyncScheduleRepository
{
    public Dictionary<int, SyncSchedule> Schedules { get; } = [];

    // Mirrors the real repository's soft-delete query filter: deleted schedules are invisible unless asked for.
    private IEnumerable<SyncSchedule> Active => Schedules.Values.Where(s => !s.IsDeleted);

    public Task<IReadOnlyList<SyncSchedule>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SyncSchedule>>(Active.ToList());

    public Task<IReadOnlyList<SyncSchedule>> ListIncludingDeletedAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SyncSchedule>>(Schedules.Values.ToList());

    public Task<SyncSchedule?> GetAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(Active.FirstOrDefault(s => s.Id == id));

    public Task<SyncSchedule?> GetIncludingDeletedAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(Schedules.GetValueOrDefault(id));

    public Task<bool> NameExistsAsync(string name, int? excludeId, CancellationToken cancellationToken) =>
        Task.FromResult(Active.Any(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) && s.Id != excludeId));

    public Task<bool> SoftDeleteAsync(int id, string user, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (Active.FirstOrDefault(s => s.Id == id) is not { } schedule)
        {
            return Task.FromResult(false);
        }

        schedule.DeletedAt = at;
        schedule.DeletedBy = user;
        schedule.IsEnabled = false;
        schedule.NextRunAt = null;
        return Task.FromResult(true);
    }

    public Task<bool> RestoreAsync(int id, string user, DateTimeOffset at, CancellationToken cancellationToken)
    {
        if (Schedules.GetValueOrDefault(id) is not { IsDeleted: true } schedule)
        {
            return Task.FromResult(false);
        }

        schedule.DeletedAt = null;
        schedule.DeletedBy = null;
        return Task.FromResult(true);
    }

    public Task<int> AddAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        schedule.Id = Schedules.Count + 1;
        Schedules[schedule.Id] = schedule;
        return Task.FromResult(schedule.Id);
    }

    public Task<bool> UpdateSettingsAsync(SyncSchedule schedule, CancellationToken cancellationToken)
    {
        var exists = Schedules.ContainsKey(schedule.Id);
        Schedules[schedule.Id] = schedule;
        return Task.FromResult(exists);
    }

    public Task<IReadOnlyList<SyncSchedule>> GetDueAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SyncSchedule>>(Active.Where(s => s.IsEnabled && s.NextRunAt <= now).ToList());

    public Task<DateTimeOffset?> GetNextRunAtAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Active.Where(s => s.IsEnabled).Min(s => s.NextRunAt));

    public Task<bool> TryClaimAsync(int id, DateTimeOffset expectedNextRunAt, DateTimeOffset newNextRunAt, DateTimeOffset startedAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RecordSuccessAsync(int id, DateTimeOffset dataEnd, CancellationToken cancellationToken)
    {
        // Mirrors the real repository: the marker only ever moves forward.
        if (Schedules[id].LastSuccessfulDataEnd is not { } current || current < dataEnd)
        {
            Schedules[id].LastSuccessfulDataEnd = dataEnd;
        }

        return Task.CompletedTask;
    }
}
