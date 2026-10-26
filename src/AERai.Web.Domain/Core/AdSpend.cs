namespace AERai.Web.Domain.Core;

/// <summary>
/// What Sponsored Products advertising of a SKU cost in one campaign on one day, and the sales Amazon
/// attributes to it. Unique per (marketplace, date, SKU, campaign).
/// </summary>
/// <remarks>
/// Read from the Amazon Ads API's advertised product report. Amazon keeps revising a day's attributed
/// sales for about a week after it, so each sync replaces whole days rather than adding to them.
/// </remarks>
public sealed class AdSpend
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>Marketplace the advertising ran in.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>The day, in the advertising account's time zone (the marketplace's).</summary>
    public DateOnly Date { get; set; }

    /// <summary>Seller SKU that was advertised.</summary>
    public required string Sku { get; set; }

    /// <summary>ASIN that was advertised, when Amazon reports it.</summary>
    public string? Asin { get; set; }

    /// <summary>Amazon Ads campaign id.</summary>
    public required string CampaignId { get; set; }

    /// <summary>Campaign name as it was on that day's report.</summary>
    public string? CampaignName { get; set; }

    /// <summary>Times the ads were shown.</summary>
    public long Impressions { get; set; }

    /// <summary>Times the ads were clicked.</summary>
    public long Clicks { get; set; }

    /// <summary>What the clicks cost, in <see cref="Currency"/>.</summary>
    public decimal Cost { get; set; }

    /// <summary>Sales Amazon attributes to the ads within 7 days of a click, in <see cref="Currency"/>.</summary>
    public decimal AttributedSales { get; set; }

    /// <summary>Orders Amazon attributes to the ads within 7 days of a click.</summary>
    public int AttributedOrders { get; set; }

    /// <summary>Units Amazon attributes to the ads within 7 days of a click.</summary>
    public int AttributedUnits { get; set; }

    /// <summary>ISO currency code of the money columns (the advertising account's currency).</summary>
    public required string Currency { get; set; }

    /// <summary>When the row was pulled from Amazon.</summary>
    public DateTimeOffset SyncedAt { get; set; }
}
