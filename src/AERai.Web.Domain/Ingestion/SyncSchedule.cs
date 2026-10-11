namespace AERai.Web.Domain.Ingestion;

/// <summary>
/// A user-managed schedule that pulls one Amazon report type into the pipeline
/// (SP-API → raw blob → staging → optionally core).
/// </summary>
public sealed class SyncSchedule
{
    /// <summary>Surrogate key.</summary>
    public int Id { get; set; }

    /// <summary>Display name, e.g. "Orders — hourly".</summary>
    public required string Name { get; set; }

    /// <summary>Which report this schedule pulls.</summary>
    public AmazonReportType ReportType { get; set; }

    /// <summary>Marketplace the schedule pulls reports for.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Whether the scheduler runs it automatically. Disabled schedules can still be run manually.</summary>
    public bool IsEnabled { get; set; }

    /// <summary>How the schedule repeats.</summary>
    public ScheduleFrequency Frequency { get; set; }

    /// <summary>Minutes between runs when <see cref="Frequency"/> is <see cref="ScheduleFrequency.Interval"/>.</summary>
    public int? IntervalMinutes { get; set; }

    /// <summary>Local time of day when <see cref="Frequency"/> is <see cref="ScheduleFrequency.Daily"/>.</summary>
    public TimeOnly? DailyTime { get; set; }

    /// <summary>
    /// Start of the local time window an <see cref="ScheduleFrequency.Interval"/> schedule runs in
    /// (in <see cref="TimeZoneId"/>). <see langword="null"/> together with <see cref="ActiveUntil"/>
    /// means around the clock.
    /// </summary>
    /// <remarks>
    /// Limiting pulls to working hours lets the serverless database pause overnight; anything
    /// needed sooner can be pulled with "Run now".
    /// </remarks>
    public TimeOnly? ActiveFrom { get; set; }

    /// <summary>
    /// End of the active window (exclusive). Earlier than <see cref="ActiveFrom"/> means the window
    /// runs past midnight, e.g. 22:00–06:00.
    /// </summary>
    public TimeOnly? ActiveUntil { get; set; }

    /// <summary>IANA time zone for <see cref="DailyTime"/> and the active window, e.g. <c>America/New_York</c>.</summary>
    public required string TimeZoneId { get; set; }

    /// <summary>
    /// How far back the first run reaches (Orders: data window; Settlements: report creation date).
    /// Later runs continue from the previous successful run.
    /// </summary>
    public int LookbackDays { get; set; }

    /// <summary>Whether each staged batch is promoted into <c>core</c> immediately.</summary>
    public bool AutoPromote { get; set; }

    /// <summary>When the scheduler should next run this schedule (UTC); <see langword="null"/> when disabled.</summary>
    public DateTimeOffset? NextRunAt { get; set; }

    /// <summary>When the schedule last started a run.</summary>
    public DateTimeOffset? LastRunAt { get; set; }

    /// <summary>
    /// End of the data window covered by the last successful run; the next Orders run starts here so
    /// no updates are missed between runs.
    /// </summary>
    public DateTimeOffset? LastSuccessfulDataEnd { get; set; }

    /// <summary>When the schedule was created.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>When the schedule settings were last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Who last changed the settings.</summary>
    public required string UpdatedBy { get; set; }

    /// <summary>Free-text notes, e.g. why the schedule exists or who relies on it.</summary>
    public string? Notes { get; set; }

    /// <summary>Email of the user responsible for this schedule; <see langword="null"/> when unassigned.</summary>
    public string? OwnerEmail { get; set; }

    /// <summary>
    /// When the schedule was deleted; <see langword="null"/> while active. Deletion is soft so run
    /// history and the "already ingested" record of each Amazon report survive (and the schedule
    /// can be restored).
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }

    /// <summary>Who deleted the schedule.</summary>
    public string? DeletedBy { get; set; }

    /// <summary>Whether the schedule has been deleted.</summary>
    public bool IsDeleted => DeletedAt is not null;
}
