using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps settlement flat-file (V2) records to <see cref="StgSettlementLine"/>.
/// </summary>
public sealed class SettlementLineMapper : IStagingRowMapper
{
    /// <inheritdoc/>
    public ImportSource Source => ImportSource.Settlements;

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredColumns { get; } = ["settlement-id", "transaction-type", "amount"];

    /// <inheritdoc/>
    public void Append(ImportBatch batch, ParsedRecord record)
    {
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(record);

        batch.SettlementLines.Add(new StgSettlementLine
        {
            RowNumber = record.RowNumber,
            RawLine = record.RawLine,
            SettlementId = record.Get("settlement-id"),
            SettlementStartDate = record.Get("settlement-start-date"),
            SettlementEndDate = record.Get("settlement-end-date"),
            PostedDate = record.Get("posted-date"),
            TransactionType = record.Get("transaction-type"),
            OrderId = record.Get("order-id"),
            Sku = record.Get("sku"),
            AmountType = record.Get("amount-type"),
            AmountDescription = record.Get("amount-description"),
            Amount = record.Get("amount"),
            Currency = record.Get("currency"),
        });
    }
}
