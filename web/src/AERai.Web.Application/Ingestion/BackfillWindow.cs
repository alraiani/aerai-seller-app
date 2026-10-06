namespace AERai.Web.Application.Ingestion;

/// <summary>
/// The history a backfill re-pulls (Orders: data window; Settlements: report creation dates).
/// </summary>
/// <param name="Start">Start of the window (inclusive).</param>
/// <param name="End">End of the window.</param>
public sealed record BackfillWindow(DateTimeOffset Start, DateTimeOffset End)
{
    /// <summary>A window ending now and reaching back a whole number of days.</summary>
    /// <param name="days">Days of history.</param>
    /// <param name="now">Current time.</param>
    /// <returns>The window.</returns>
    public static BackfillWindow LastDays(int days, DateTimeOffset now) => new(now.AddDays(-days), now);

    /// <summary>Length of the window.</summary>
    public TimeSpan Span => End - Start;

    /// <summary>Human-readable description for run history, e.g. "2026-09-01 to 2026-09-15".</summary>
    /// <returns>The description.</returns>
    public override string ToString() =>
        $"{Start.UtcDateTime:yyyy-MM-dd} to {End.UtcDateTime:yyyy-MM-dd}";
}
