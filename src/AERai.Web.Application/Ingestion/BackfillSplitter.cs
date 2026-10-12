namespace AERai.Web.Application.Ingestion;

/// <summary>Cuts a long backfill range into windows a report can cover. Pure, so it is unit-tested alone.</summary>
public static class BackfillSplitter
{
    /// <summary>Splits a range into consecutive windows of at most <paramref name="maxDays"/> days, oldest first.</summary>
    /// <param name="window">The whole range.</param>
    /// <param name="maxDays">Longest window.</param>
    /// <returns>Windows that join end to start and together cover exactly <paramref name="window"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxDays"/> is less than one.</exception>
    public static IReadOnlyList<BackfillWindow> Split(BackfillWindow window, int maxDays)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxDays, 1);

        var step = TimeSpan.FromDays(maxDays);
        var windows = new List<BackfillWindow>();
        for (var start = window.Start; start < window.End; start += step)
        {
            var end = start + step;
            windows.Add(new BackfillWindow(start, end < window.End ? end : window.End));
        }

        return windows;
    }
}
