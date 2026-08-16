namespace AERai.Seller.Domain;

/// <summary>
/// Amazon Seller Central's order/business-report widgets use Pacific Time as the "today"
/// boundary for North American marketplaces — not UTC and not the seller's own local time.
/// Confirmed by reconciling this app's synced order data against Seller Central's reported
/// unit count: using a UTC calendar day over-counted by exactly the UTC-00:00-to-07:00 slice
/// that Pacific Time still considers "yesterday". Centralized here so sync, repository, and
/// ViewModel layers all agree on the same boundary.
/// </summary>
public static class AmazonBusinessDay
{
    private static readonly TimeZoneInfo PacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/Los_Angeles");

    public static DateOnly TodayIn(DateTimeOffset utcNow) => DateOf(utcNow);

    public static DateOnly DateOf(DateTimeOffset instant)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, PacificTimeZone).DateTime);

    /// <summary>The UTC instant corresponding to Pacific-Time midnight at the start of <paramref name="date"/>.</summary>
    public static DateTimeOffset StartOfDayUtc(DateOnly date)
    {
        var wallClockMidnight = date.ToDateTime(TimeOnly.MinValue);
        var offset = PacificTimeZone.GetUtcOffset(wallClockMidnight);
        return new DateTimeOffset(wallClockMidnight, offset);
    }
}
