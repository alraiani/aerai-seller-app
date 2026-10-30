namespace AERai.Web.Application.Inventory;

/// <summary>What the shipment review knows about one SKU from the catalog and home stock.</summary>
/// <param name="Sku">Seller SKU, as the catalog spells it.</param>
/// <param name="Title">Product title.</param>
/// <param name="FamilyId">The SKU's family, if any.</param>
/// <param name="FamilyName">The family's name.</param>
/// <param name="ShipsFromHomeStock">Whether the family ships into Amazon from home stock.</param>
/// <param name="HomeStock">Units at home now in the marketplace.</param>
public sealed record InboundSkuInfo(string Sku, string? Title, int? FamilyId, string? FamilyName, bool ShipsFromHomeStock, int HomeStock);
