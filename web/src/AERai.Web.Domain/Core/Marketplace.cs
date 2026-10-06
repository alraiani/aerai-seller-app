namespace AERai.Web.Domain.Core;

/// <summary>
/// An Amazon marketplace the business sells in. Every order, inventory snapshot, settlement, import,
/// and sync schedule belongs to exactly one marketplace, and the pages show one marketplace at a time.
/// </summary>
public sealed class Marketplace
{
    /// <summary>Amazon's marketplace id (natural key), e.g. <c>ATVPDKIKX0DER</c>.</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Short code shown in the switcher, e.g. <c>US</c>.</summary>
    public required string Code { get; set; }

    /// <summary>Display name, e.g. <c>United States</c>.</summary>
    public required string Name { get; set; }

    /// <summary>SP-API region whose endpoint and credentials serve this marketplace.</summary>
    public AmazonRegion Region { get; set; }

    /// <summary>ISO 4217 currency the marketplace sells and settles in.</summary>
    public required string Currency { get; set; }

    /// <summary>IANA time zone used for the marketplace's local business days.</summary>
    public required string TimeZoneId { get; set; }

    /// <summary>
    /// Value of the orders report's <c>sales-channel</c> column for this marketplace
    /// (e.g. <c>Amazon.com</c>); promotion uses it to keep other channels' lines out.
    /// </summary>
    public required string SalesChannel { get; set; }

    /// <summary>Order in the switcher.</summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Whether the marketplace is in use. Inactive marketplaces show in the switcher as not set up
    /// and cannot be selected.
    /// </summary>
    public bool IsActive { get; set; }
}
