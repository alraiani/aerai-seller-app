namespace AERai.Web.Application.Ingestion;

/// <summary>
/// How long the background scheduler may sleep before something is due.
/// </summary>
public static class SchedulerSleep
{
    /// <summary>
    /// The time until the earliest upcoming event, capped at <paramref name="maxSleep"/>. Events
    /// already past return <see cref="TimeSpan.Zero"/> so they run immediately.
    /// </summary>
    /// <param name="now">Current time.</param>
    /// <param name="maxSleep">Longest allowed sleep.</param>
    /// <param name="events">Candidate event times; <see langword="null"/> means "none of this kind".</param>
    /// <returns>The sleep duration.</returns>
    public static TimeSpan Until(DateTimeOffset now, TimeSpan maxSleep, params ReadOnlySpan<DateTimeOffset?> events)
    {
        var sleep = maxSleep;
        foreach (var at in events)
        {
            if (at is { } due && due - now < sleep)
            {
                sleep = due - now;
            }
        }

        return sleep < TimeSpan.Zero ? TimeSpan.Zero : sleep;
    }
}
