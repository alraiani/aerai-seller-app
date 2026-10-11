namespace AERai.Web.Domain.Ingestion;

/// <summary>
/// App-wide sync settings, stored as a single row. Pausing is global and deliberately separate from
/// each schedule's own on/off switch, so resuming restores exactly the schedules that were on.
/// </summary>
public sealed class SyncSettings
{
    /// <summary>The id of the one settings row.</summary>
    public const int SingletonId = 1;

    /// <summary>Always <see cref="SingletonId"/>.</summary>
    public int Id { get; set; } = SingletonId;

    /// <summary>Whether scheduled runs are paused. Manual runs and backfills still work.</summary>
    public bool IsPaused { get; set; }

    /// <summary>When syncs were paused.</summary>
    public DateTimeOffset? PausedAt { get; set; }

    /// <summary>Who paused syncs.</summary>
    public string? PausedBy { get; set; }
}
