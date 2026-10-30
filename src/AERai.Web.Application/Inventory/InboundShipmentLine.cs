namespace AERai.Web.Application.Inventory;

/// <summary>One SKU of an Amazon shipment, as the review shows it.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Title">Product title, when the SKU is in the catalog.</param>
/// <param name="FamilyId">The SKU's family, if any.</param>
/// <param name="FamilyName">The family's name.</param>
/// <param name="FromHome">Whether the SKU's family ships from home stock, so its units come off home stock.</param>
/// <param name="AmazonUnits">Units of the SKU Amazon has in the shipment now.</param>
/// <param name="HomeStock">Units of the SKU at home now.</param>
/// <param name="LoggedUnits">Units already logged as sent under the shipment's FBA id.</param>
/// <param name="Suggested">Units the review suggests logging now: Amazon's, up to the home stock, nothing once logged.</param>
public sealed record InboundShipmentLine(
    string Sku,
    string? Title,
    int? FamilyId,
    string? FamilyName,
    bool FromHome,
    int AmazonUnits,
    int HomeStock,
    int LoggedUnits,
    int Suggested)
{
    /// <summary>Whether there are fewer units at home than Amazon expects (and none are logged yet).</summary>
    public bool ShortAtHome => FromHome && LoggedUnits == 0 && HomeStock < AmazonUnits;

    /// <summary>Whether what was logged differs from what Amazon now has in the shipment.</summary>
    public bool DiffersFromAmazon => FromHome && LoggedUnits > 0 && LoggedUnits != AmazonUnits;
}
