using AERai.Web.Application.Common;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Schedule management use cases behind the Schedules page.
/// </summary>
public interface ISyncScheduleService
{
    /// <summary>Validates and creates a schedule.</summary>
    /// <param name="input">Settings.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new id, or the validation failure.</returns>
    Task<Result<int>> CreateAsync(SyncScheduleInput input, string user, CancellationToken cancellationToken);

    /// <summary>Validates and updates a schedule, recomputing its next run.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="input">Settings.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the validation/not-found failure.</returns>
    Task<Result> UpdateAsync(int id, SyncScheduleInput input, string user, CancellationToken cancellationToken);

    /// <summary>Turns automatic runs on or off.</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="enabled">Desired state.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a not-found failure.</returns>
    Task<Result> SetEnabledAsync(int id, bool enabled, string user, CancellationToken cancellationToken);

    /// <summary>Queues an immediate run (processed by the background worker).</summary>
    /// <param name="id">Schedule id.</param>
    /// <param name="user">Acting user.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure when the schedule is unknown or Amazon is not connected.</returns>
    Task<Result> RunNowAsync(int id, string user, CancellationToken cancellationToken);

    /// <summary>Builds a schedule from validated input (used for create and to preview next runs).</summary>
    /// <param name="input">Settings.</param>
    /// <returns>The schedule, or the first validation failure.</returns>
    Result<SyncSchedule> Validate(SyncScheduleInput input);
}
