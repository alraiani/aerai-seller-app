using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Bookkeeping;

/// <summary>
/// Default <see cref="IBookkeepingService"/>: previews a settlement against the account mapping, refuses
/// to export while anything is unmapped, generates the IIF text, and stores it for download.
/// </summary>
/// <param name="queries">Settlement totals, mappings, and stored exports.</param>
/// <param name="store">Writes mappings, settings, and exports.</param>
/// <param name="timeProvider">Clock for timestamps and file names.</param>
/// <param name="logger">Logger.</param>
public sealed partial class BookkeepingService(IBookkeepingQueries queries, IBookkeepingStore store, TimeProvider timeProvider, ILogger<BookkeepingService> logger)
    : IBookkeepingService
{
    /// <summary>Longest account name QuickBooks accepts, with room to spare.</summary>
    public const int MaxAccountNameLength = 200;

    /// <summary>Most generated files listed at once.</summary>
    public const int MaxListedExports = 50;

    /// <inheritdoc/>
    public async Task<Result<SettlementPreview>> PreviewAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(settlementId);

        var settlement = await queries.GetSettlementAsync(marketplaceId, settlementId, cancellationToken).ConfigureAwait(false);
        if (settlement is null)
        {
            return Result.Failure<SettlementPreview>($"Settlement {settlementId} was not found in this marketplace.");
        }

        var mappings = await queries.GetMappingsAsync(cancellationToken).ConfigureAwait(false);
        var deposit = await queries.GetDepositAccountAsync(marketplaceId, cancellationToken).ConfigureAwait(false);
        return Result.Success(SettlementPreviewBuilder.Build(settlement, mappings, deposit));
    }

    /// <inheritdoc/>
    public async Task<Result<BookkeepingExportSummary>> ExportAsync(string marketplaceId, string settlementId, string requestedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);

        var previewed = await PreviewAsync(marketplaceId, settlementId, cancellationToken).ConfigureAwait(false);
        if (previewed.IsFailure)
        {
            return Result.Failure<BookkeepingExportSummary>(previewed.Error);
        }

        var preview = previewed.Value;
        if (preview.Rows.Count == 0)
        {
            return Result.Failure<BookkeepingExportSummary>($"Settlement {settlementId} has no lines to export.");
        }

        if (preview.HasUnmapped)
        {
            var count = preview.Unmapped.Count;
            return Result.Failure<BookkeepingExportSummary>(
                $"Settlement {settlementId} has {count} unmapped kind{(count == 1 ? "" : "s")} of amount. Map {(count == 1 ? "it" : "them")} on the Account mapping page before exporting.");
        }

        if (!preview.HasDepositAccount)
        {
            return Result.Failure<BookkeepingExportSummary>("Set this marketplace's deposit account on the Account mapping page before exporting.");
        }

        var generatedAt = timeProvider.GetUtcNow();
        var export = new BookkeepingExport
        {
            MarketplaceId = marketplaceId,
            SettlementId = settlementId,
            FileName = string.Create(CultureInfo.InvariantCulture, $"Amazon-Settlement-{settlementId}-{generatedAt:yyyyMMddHHmmss}.iif"),
            Content = IifExportGenerator.Generate(preview),
            DepositAccountName = preview.DepositAccountName!, // Non-null: HasDepositAccount was checked above.
            NetTotal = preview.NetTotal,
            Currency = preview.Currency,
            PeriodStart = preview.PeriodStart,
            PeriodEnd = preview.PeriodEnd,
            GeneratedAt = generatedAt,
            GeneratedBy = requestedBy,
        };

        var id = await store.AddExportAsync(export, cancellationToken).ConfigureAwait(false);
        LogExported(settlementId, marketplaceId, export.NetTotal, requestedBy);
        return Result.Success(new BookkeepingExportSummary(id, settlementId, export.FileName, export.NetTotal, export.Currency, generatedAt, requestedBy));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<BookkeepingExportSummary>> GetExportsAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        return await queries.GetExportsAsync(marketplaceId, MaxListedExports, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<BookkeepingExportFile?> GetExportFileAsync(string marketplaceId, long exportId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        return queries.GetExportFileAsync(marketplaceId, exportId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<AccountMappingView> GetMappingViewAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);

        var categories = await queries.GetCategoriesAsync(cancellationToken).ConfigureAwait(false);
        var mappings = await queries.GetMappingsAsync(cancellationToken).ConfigureAwait(false);
        var deposit = await queries.GetDepositAccountAsync(marketplaceId, cancellationToken).ConfigureAwait(false);

        var accounts = mappings.ToDictionary(m => (m.AmountType, m.AmountDescription), m => m.QuickBooksAccountName);
        var rows = categories
            .Select(c => new AccountMappingRow(c.AmountType, c.AmountDescription, accounts.GetValueOrDefault((c.AmountType, c.AmountDescription)), c.LineCount))
            .OrderBy(r => string.IsNullOrWhiteSpace(r.QuickBooksAccountName) ? 0 : 1)
            .ThenBy(r => r.AmountType, StringComparer.Ordinal)
            .ThenBy(r => r.AmountDescription, StringComparer.Ordinal)
            .ToList();
        return new AccountMappingView(rows, deposit);
    }

    /// <inheritdoc/>
    public async Task<Result> SaveMappingsAsync(
        string marketplaceId, string? depositAccountName, IReadOnlyList<AccountMappingInput> mappings, string requestedBy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(mappings);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedBy);

        var deposit = depositAccountName?.Trim();
        var cleaned = mappings
            .Select(m => m with { QuickBooksAccountName = m.QuickBooksAccountName?.Trim() })
            .ToList();

        // A tab or line break in a name would split an IIF line and corrupt the import, so reject it here.
        foreach (var name in cleaned.Select(m => m.QuickBooksAccountName).Append(deposit))
        {
            if (name is null)
            {
                continue;
            }

            if (name.Length > MaxAccountNameLength)
            {
                return Result.Failure($"Account names can be at most {MaxAccountNameLength} characters ('{name[..30]}…' is longer).");
            }

            if (name.Any(char.IsControl))
            {
                return Result.Failure($"Account name '{name}' contains a tab or line break.");
            }
        }

        var now = timeProvider.GetUtcNow();
        await store.SaveMappingsAsync(cleaned, now, requestedBy, cancellationToken).ConfigureAwait(false);
        await store.SaveDepositAccountAsync(marketplaceId, deposit, now, requestedBy, cancellationToken).ConfigureAwait(false);
        LogMappingsSaved(cleaned.Count, marketplaceId, requestedBy);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Exported settlement {SettlementId} ({MarketplaceId}) net {NetTotal} for {User}")]
    private partial void LogExported(string settlementId, string marketplaceId, decimal netTotal, string user);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved {Count} account mappings and the {MarketplaceId} deposit account for {User}")]
    private partial void LogMappingsSaved(int count, string marketplaceId, string user);
}
