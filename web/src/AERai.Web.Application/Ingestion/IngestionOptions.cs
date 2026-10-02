using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Background ingestion tunables. Bound from the <c>Ingestion</c> configuration section.
/// </summary>
public sealed class IngestionOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Ingestion";

    /// <summary>
    /// Whether this app instance runs the background scheduler. Turn off on extra instances if you
    /// want exactly one worker (runs are claimed atomically, so leaving it on everywhere is also safe).
    /// </summary>
    public bool SchedulerEnabled { get; set; } = true;

    /// <summary>How often the scheduler checks for due schedules.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:10:00")]
    public TimeSpan SchedulerTick { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Delay between report status checks while Amazon generates a report.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan ReportPollInterval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Give up waiting for a report after this long.</summary>
    [Range(typeof(TimeSpan), "00:01:00", "04:00:00")]
    public TimeSpan ReportMaxWait { get; set; } = TimeSpan.FromMinutes(45);
}
