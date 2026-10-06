namespace AERai.Web.Domain.Alerts;

/// <summary>Records that one user has read one alert (read state is per user).</summary>
public sealed class StockAlertRead
{
    /// <summary>The alert.</summary>
    public long StockAlertId { get; set; }

    /// <summary>The user's email (their sign-in name).</summary>
    public required string UserEmail { get; set; }

    /// <summary>When they marked it read.</summary>
    public DateTimeOffset ReadAt { get; set; }
}
