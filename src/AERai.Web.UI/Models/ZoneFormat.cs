namespace AERai.Web.UI.Models;

/// <summary>Names a marketplace's time zone the way people say it, for "times in …" notes and date labels.</summary>
public static class ZoneFormat
{
    /// <summary>A friendly name for an IANA time zone.</summary>
    /// <param name="timeZoneId">IANA zone id, e.g. <c>America/New_York</c>.</param>
    /// <returns>e.g. "Eastern time", "UK time"; otherwise the city, e.g. "Mexico City time".</returns>
    public static string Name(string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);
        return timeZoneId switch
        {
            "America/New_York" or "America/Toronto" => "Eastern time",
            "America/Chicago" or "America/Mexico_City" => "Central time",
            "America/Denver" => "Mountain time",
            "America/Phoenix" => "Arizona time",
            "America/Los_Angeles" or "America/Vancouver" => "Pacific time",
            "Europe/London" => "UK time",
            "UTC" or "Etc/UTC" => "UTC",
            _ => timeZoneId.Split('/')[^1].Replace('_', ' ') + " time",
        };
    }
}
