namespace AERai.Seller.Domain.Staging;

/// <summary>
/// A parent ASIN (variation family). Amazon's Catalog Items API returns parent-level
/// data once per family, so it's normalized into its own table rather than repeated
/// on every child <see cref="CatalogItem"/> row. Standalone (non-variation) ASINs have
/// no corresponding row here — <see cref="CatalogItem.ParentAsin"/> is null instead.
/// </summary>
public class CatalogParent
{
    public required string ParentAsin { get; set; }
    public string? Title { get; set; }
    public DateTimeOffset SyncedAt { get; set; }

    public List<CatalogItem> Items { get; set; } = [];
}
