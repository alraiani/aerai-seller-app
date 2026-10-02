using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="ISyncScheduleRepository"/>.</summary>
internal sealed class FakeSyncScheduleRepository : ISyncScheduleRepository
{
    public Dictionary<int, SyncSchedule> Schedules { get; } = [];

    public Task<IReadOnlyList<SyncSchedule>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SyncSchedule>>(Schedules.Values.ToList());

    public Task<SyncSchedule?> GetAsync(int id, CancellationToken cancellationToken) =>
        Task.FromResult(Schedules.GetValueOrDefault(id));

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
        Task.FromResult<IReadOnlyList<SyncSchedule>>(Schedules.Values.Where(s => s.IsEnabled && s.NextRunAt <= now).ToList());

    public Task<bool> TryClaimAsync(int id, DateTimeOffset expectedNextRunAt, DateTimeOffset newNextRunAt, DateTimeOffset startedAt, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task RecordSuccessAsync(int id, DateTimeOffset dataEnd, CancellationToken cancellationToken)
    {
        Schedules[id].LastSuccessfulDataEnd = dataEnd;
        return Task.CompletedTask;
    }
}
