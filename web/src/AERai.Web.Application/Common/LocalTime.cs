namespace AERai.Web.Application.Common;

/// <summary>
/// Converts between instants and a marketplace's local business calendar, so days start at local
/// midnight rather than UTC midnight.
/// </summary>
public static class LocalTime
{
    /// <summary>The local calendar date at an instant.</summary>
    /// <param name="instant">The instant.</param>
    /// <param name="zone">Business time zone.</param>
    /// <returns>The local date.</returns>
    public static DateOnly DateOf(DateTimeOffset instant, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);
    }

    /// <summary>
    /// Converts a local wall-clock time to an instant. Times that fall in a daylight-saving gap move
    /// forward an hour; repeated (fall-back) times take their first occurrence.
    /// </summary>
    /// <param name="localWallTime">Local date and time.</param>
    /// <param name="zone">Business time zone.</param>
    /// <param name="toLocal">Return the instant expressed with the local offset (for display) instead of UTC.</param>
    /// <returns>The instant.</returns>
    public static DateTimeOffset ToInstant(DateTime localWallTime, TimeZoneInfo zone, bool toLocal = false)
    {
        ArgumentNullException.ThrowIfNull(zone);

        var wall = DateTime.SpecifyKind(localWallTime, DateTimeKind.Unspecified);
        if (zone.IsInvalidTime(wall))
        {
            wall = wall.AddHours(1);
        }

        var offset = zone.IsAmbiguousTime(wall) ? zone.GetAmbiguousTimeOffsets(wall).Max() : zone.GetUtcOffset(wall);
        var instant = new DateTimeOffset(wall, offset);
        return toLocal ? instant : instant.ToUniversalTime();
    }

    /// <summary>The instant a local day starts (local midnight), in UTC.</summary>
    /// <param name="day">Local date.</param>
    /// <param name="zone">Business time zone.</param>
    /// <returns>The instant.</returns>
    public static DateTimeOffset StartOfDay(DateOnly day, TimeZoneInfo zone) => ToInstant(day.ToDateTime(TimeOnly.MinValue), zone);
}
