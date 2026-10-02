using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps flat order-report records to <see cref="StgOrderLine"/>.
/// </summary>
public sealed class OrderLineMapper : IStagingRowMapper
{
    /// <inheritdoc/>
    public ImportSource Source => ImportSource.Orders;

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredColumns { get; } =
        ["amazon-order-id", "purchase-date", "order-status", "sku", "quantity", "item-price"];

    /// <inheritdoc/>
    public void Append(ImportBatch batch, ParsedRecord record)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(record);

        batch.OrderLines.Add(new StgOrderLine
        {
            RowNumber = record.RowNumber,
            RawLine = record.RawLine,
            AmazonOrderId = record.Get("amazon-order-id"),
            PurchaseDate = record.Get("purchase-date"),
            OrderStatus = record.Get("order-status"),
            Sku = record.Get("sku"),
            Asin = record.Get("asin"),
            ProductName = record.Get("product-name"),
            Quantity = record.Get("quantity"),
            ItemPrice = record.Get("item-price"),
            Currency = record.Get("currency"),
        });
    }
}
