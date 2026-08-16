using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Bookkeeping;

public interface IBookkeepingExportService
{
    Task<SettlementSummary> PreviewAsync(string settlementId, CancellationToken cancellationToken = default);

    /// <summary>Throws InvalidOperationException if the settlement still has unmapped categories.</summary>
    Task<BookkeepingExportRecord> ExportAsync(string settlementId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Orchestrates settlement preview and IIF export: builds a SettlementSummary, refuses to export
/// while categories are unmapped, generates the IIF text, saves it via IExportFileStore, and records
/// the export via IBookkeepingExportRepository.
/// </summary>
public sealed class BookkeepingExportService(
    ISettlementRepository settlementRepository,
    IBookkeepingAccountMappingRepository mappingRepository,
    IBookkeepingSettingsRepository settingsRepository,
    IBookkeepingExportRepository exportRepository,
    IExportFileStore fileStore,
    TimeProvider timeProvider) : IBookkeepingExportService
{
    public async Task<SettlementSummary> PreviewAsync(string settlementId, CancellationToken cancellationToken = default)
    {
        var (_, summary) = await BuildSummaryAsync(settlementId, cancellationToken);
        return summary;
    }

    public async Task<BookkeepingExportRecord> ExportAsync(string settlementId, CancellationToken cancellationToken = default)
    {
        var (settlement, summary) = await BuildSummaryAsync(settlementId, cancellationToken);

        if (summary.HasUnmappedCategories)
        {
            throw new InvalidOperationException(
                $"Settlement {settlementId} has {summary.UnmappedCategories.Count} unmapped " +
                $"categor{(summary.UnmappedCategories.Count == 1 ? "y" : "ies")} — map them on the Account Mapping page before exporting.");
        }

        var settings = await settingsRepository.GetAsync(cancellationToken);
        var content = IifExportGenerator.Generate(summary, settlement, settings.DepositAccountName ?? string.Empty);

        var generatedAt = timeProvider.GetUtcNow();
        var fileName = $"Amazon-Settlement-{settlementId}-{generatedAt:yyyyMMddHHmmss}.iif";
        var filePath = await fileStore.SaveAsync(fileName, content, cancellationToken);

        var record = new BookkeepingExportRecord
        {
            SettlementId = settlementId,
            FileName = fileName,
            FilePath = filePath,
            GeneratedAt = generatedAt,
            NetTotal = summary.NetTotal,
            Currency = summary.Currency,
            PeriodStart = settlement.FinancialEventGroupStart,
            PeriodEnd = settlement.FinancialEventGroupEnd,
            DepositDate = settlement.DepositDate,
        };
        await exportRepository.InsertAsync(record, cancellationToken);

        return record;
    }

    private async Task<(SettlementReport Settlement, SettlementSummary Summary)> BuildSummaryAsync(
        string settlementId, CancellationToken cancellationToken)
    {
        var settlements = await settlementRepository.GetAllAsync(cancellationToken);
        var settlement = settlements.FirstOrDefault(s => s.SettlementId == settlementId)
            ?? throw new InvalidOperationException($"Settlement {settlementId} not found.");

        var lineItems = await settlementRepository.GetLineItemsAsync(settlementId, cancellationToken);
        var mappings = await mappingRepository.GetAllAsync(cancellationToken);

        var summary = SettlementSummaryBuilder.Build(settlement, lineItems, mappings);
        return (settlement, summary);
    }
}
