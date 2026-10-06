namespace AERai.Web.Domain.Alerts;

/// <summary>
/// An in-app notification that a SKU is running low or is out of stock in one marketplace. At most
/// one alert per (marketplace, SKU) is open at a time; when the situation clears it is resolved, and
/// a change of level resolves it and raises a new one, so users are notified again.
/// </summary>
public sealed class StockAlert
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Marketplace the alert is for.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Seller SKU.</summary>
    public required string Sku { get; set; }

    /// <summary>How serious it is.</summary>
    public StockAlertLevel Level { get; set; }

    /// <summary>Plain-English detail (refreshed while open, e.g. as a deadline gets closer).</summary>
    public required string Message { get; set; }

    /// <summary>When the alert was raised.</summary>
    public DateTimeOffset RaisedAt { get; set; }

    /// <summary>When the message was last refreshed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>When the situation cleared, or <see langword="null"/> while open.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }
}
