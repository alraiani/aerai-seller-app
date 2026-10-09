namespace AERai.Web.Domain.Ingestion;

/// <summary>
/// One execution of a <see cref="SyncSchedule"/>: the audit trail from Amazon report to staging batch.
/// </summary>
public sealed class SyncRun
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>The schedule that was run.</summary>
    public int SyncScheduleId { get; set; }

    /// <summary>Report type pulled.</summary>
    public AmazonReportType ReportType { get; set; }

    /// <summary>Marketplace of the schedule at the time of the run.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>What started the run.</summary>
    public SyncTrigger Trigger { get; set; }

    /// <summary>User who clicked "Run now", or <c>scheduler</c>.</summary>
    public required string TriggeredBy { get; set; }

    /// <summary>When the run started.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the run finished; <see langword="null"/> while running.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Outcome.</summary>
    public SyncRunStatus Status { get; set; } = SyncRunStatus.Running;

    /// <summary>Start of the requested data window (on-demand reports only).</summary>
    public DateTimeOffset? DataStart { get; set; }

    /// <summary>End of the requested data window (on-demand reports only).</summary>
    public DateTimeOffset? DataEnd { get; set; }

    /// <summary>Amazon report id(s) processed, comma-separated (for support tickets with Amazon).</summary>
    public string? AmazonReportIds { get; set; }

    /// <summary>Staging batch id(s) created, comma-separated.</summary>
    public string? ImportBatchIds { get; set; }

    /// <summary>Human-readable summary or error.</summary>
    public string? Message { get; set; }

    /// <summary>
    /// The Amazon report this run is waiting for, while Amazon generates it; <see langword="null"/>
    /// when the run is not waiting (settlement listings, or once finished).
    /// </summary>
    /// <remarks>
    /// Persisting the wait (instead of holding it in memory) lets the scheduler do other work in the
    /// meantime and lets a restart pick the run up where it left off.
    /// </remarks>
    public string? PendingReportId { get; set; }

    /// <summary>When to next ask Amazon about <see cref="PendingReportId"/>.</summary>
    public DateTimeOffset? NextCheckAt { get; set; }

    /// <summary>Status checks so far that found the report still being generated (drives the backoff).</summary>
    public int PollAttempts { get; set; }

    /// <summary>Start of the backfill window, when this run is a backfill.</summary>
    public DateTimeOffset? BackfillStart { get; set; }

    /// <summary>End of the backfill window, when this run is a backfill.</summary>
    public DateTimeOffset? BackfillEnd { get; set; }
}
