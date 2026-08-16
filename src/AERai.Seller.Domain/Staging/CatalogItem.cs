namespace AERai.Seller.Domain.Staging;

/// <summary>
/// One catalog entry (child ASIN) as returned by Amazon's Catalog Items API.
/// <see cref="ParentAsin"/> is a nullable FK into <see cref="CatalogParent"/> — populated
/// for ASINs that belong to a variation family, null for standalone ASINs.
/// </summary>
public class CatalogItem
{
    public required string Asin { get; set; }
    public string? ParentAsin { get; set; }
    public string? Sku { get; set; }
    public string? Title { get; set; }
    public string? Brand { get; set; }
    public string? ImageUrl { get; set; }
    public DateTimeOffset SyncedAt { get; set; }
}
