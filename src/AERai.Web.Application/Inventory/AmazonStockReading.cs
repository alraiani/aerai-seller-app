namespace AERai.Web.Application.Inventory;

/// <summary>A SKU's FBA stock in its latest main snapshot, as compared with the sends pending to Amazon.</summary>
/// <param name="SellThroughStock">Available, inbound, and FC transfers and processing.</param>
/// <param name="Inbound">Inbound units, whatever their stage.</param>
/// <param name="ReceivedAt">When the snapshot was received; only sends logged before it can be in it.</param>
public sealed record AmazonStockReading(int SellThroughStock, int Inbound, DateTimeOffset ReceivedAt);
