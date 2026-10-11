namespace AERai.Web.Application.Inventory;

/// <summary>Units of one SKU sent from home stock into Amazon (see <see cref="IHomeStockLedgerService.ShipToAmazonAsync"/>).</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="Current">Home stock the user saw when entering the amount; the send is skipped if it has changed since.</param>
/// <param name="Units">Units sent (positive, at most <paramref name="Current"/>).</param>
public sealed record HomeStockShipment(string Sku, int Current, int Units);
