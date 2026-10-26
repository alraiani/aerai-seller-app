namespace AERai.Web.Application.Ingestion;

/// <summary>One SKU's Sponsored Products spend in one campaign on one day, as Amazon Ads reports it.</summary>
/// <param name="Date">The day, in the advertising account's time zone.</param>
/// <param name="Sku">Advertised seller SKU.</param>
/// <param name="Asin">Advertised ASIN, when reported.</param>
/// <param name="CampaignId">Campaign id.</param>
/// <param name="CampaignName">Campaign name, when reported.</param>
/// <param name="Impressions">Times the ads were shown.</param>
/// <param name="Clicks">Times the ads were clicked.</param>
/// <param name="Cost">What the clicks cost.</param>
/// <param name="AttributedSales">Sales attributed to the ads within 7 days of a click.</param>
/// <param name="AttributedOrders">Orders attributed to the ads within 7 days of a click.</param>
/// <param name="AttributedUnits">Units attributed to the ads within 7 days of a click.</param>
/// <param name="Currency">Currency of the money columns, when reported (else the marketplace's).</param>
public sealed record AdSpendRow(
    DateOnly Date,
    string Sku,
    string? Asin,
    string CampaignId,
    string? CampaignName,
    long Impressions,
    long Clicks,
    decimal Cost,
    decimal AttributedSales,
    int AttributedOrders,
    int AttributedUnits,
    string? Currency);
