using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Products;

/// <summary>
/// Default <see cref="IProductService"/>.
/// </summary>
/// <param name="repository">Product persistence.</param>
/// <param name="spreadsheets">Reads uploaded .xlsx cost sheets.</param>
/// <param name="timeProvider">Clock for the update timestamp.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ProductService(IProductRepository repository, ISpreadsheetReader spreadsheets, TimeProvider timeProvider, ILogger<ProductService> logger)
    : IProductService
{
    /// <summary>Sanity ceiling on unit cost; anything higher is almost certainly a typo.</summary>
    public const decimal MaxCostOfGoods = 100_000m;

    /// <summary>Most data rows accepted in one cost sheet (well above the catalog size).</summary>
    public const int MaxCostSheetRows = 10_000;

    /// <summary>The columns a cost sheet must have, as normalized headers.</summary>
    public static IReadOnlyList<string> CostSheetColumns { get; } = ["sku", "cost-of-goods"];

    /// <inheritdoc/>
    public async Task<Result> UpdateCostAsync(string sku, string marketplaceId, decimal? costOfGoods, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);

        if (costOfGoods is < 0 or > MaxCostOfGoods)
        {
            return Result.Failure($"Cost of goods must be between 0 and {MaxCostOfGoods:N0}.");
        }

        // Round to cents up front so the stored value matches what the user sees after save.
        var rounded = costOfGoods is null ? (decimal?)null : ProductCostText.Round(costOfGoods.Value);

        var updated = await repository.UpdateCostAsync(sku, marketplaceId, rounded, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (!updated)
        {
            return Result.Failure($"Product '{sku}' was not found.");
        }

        LogCostUpdated(sku, marketplaceId, rounded);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result<ProductCostResult>> SaveCostsAsync(string marketplaceId, IReadOnlyList<ProductCostChange> changes, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(changes);

        if (changes.FirstOrDefault(c => c.New is < 0 or > MaxCostOfGoods) is { } bad)
        {
            return Result.Failure<ProductCostResult>($"Cost of goods for {bad.Sku} must be between 0 and {MaxCostOfGoods:N0}.");
        }

        // The same SKU twice keeps the last change, like a sheet read top to bottom.
        var unique = changes
            .GroupBy(c => c.Sku, StringComparer.Ordinal)
            .Select(g => g.Last())
            .Select(c => c with { Current = Round(c.Current), New = Round(c.New) })
            .Where(c => c.Current != c.New)
            .ToList();
        if (unique.Count == 0)
        {
            return Result.Success(new ProductCostResult(0, []));
        }

        var stale = await repository.ApplyCostsAsync(marketplaceId, unique, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        var applied = unique.Count - stale.Count;

        LogCostsSaved(applied, stale.Count, marketplaceId);
        return Result.Success(new ProductCostResult(applied, stale));
    }

    /// <inheritdoc/>
    public async Task<Result<ProductCostReconciliation>> PreviewCostImportAsync(string marketplaceId, string fileName, Stream content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var parsed = await UploadedSheet.ReadAsync(spreadsheets, fileName, content, MaxCostSheetRows, cancellationToken).ConfigureAwait(false);
        if (parsed.IsFailure)
        {
            return Result.Failure<ProductCostReconciliation>(parsed.Error);
        }

        if (CostSheetColumns.FirstOrDefault(c => !parsed.Value.Headers.Contains(c, StringComparer.Ordinal)) is { } missing)
        {
            return Result.Failure<ProductCostReconciliation>($"The file has no '{missing}' column. Use the cost sheet: columns sku and cost-of-goods.");
        }

        var records = parsed.Value.Records;
        var current = await repository.GetCostsAsync(
            marketplaceId, records.Select(r => r.Get("sku")).OfType<string>().Distinct(StringComparer.Ordinal).ToList(), cancellationToken).ConfigureAwait(false);

        var rejected = new List<RejectedRow>();
        var costed = new List<(string Sku, decimal Cost)>();
        var blank = 0;
        foreach (var record in records)
        {
            var sku = record.Get("sku");
            var costText = record.Get("cost-of-goods");
            if (sku is null)
            {
                rejected.Add(new RejectedRow(record.RowNumber, "Missing sku.", record.RawLine));
            }
            else if (!current.ContainsKey(sku))
            {
                rejected.Add(new RejectedRow(record.RowNumber, $"Unknown SKU '{sku}'.", record.RawLine));
            }
            else if (!ProductCostText.TryParse(costText, out var cost))
            {
                rejected.Add(new RejectedRow(record.RowNumber, $"cost-of-goods must be a number from 0 to {MaxCostOfGoods:N0} (got '{costText}').", record.RawLine));
            }
            else if (cost is null)
            {
                blank++;
            }
            else
            {
                costed.Add((sku, cost.Value));
            }
        }

        // The same SKU twice keeps the last row, like a sheet read top to bottom.
        var latest = costed.GroupBy(c => c.Sku, StringComparer.Ordinal).Select(g => g.Last()).ToList();
        var changes = latest
            .Select(c => (Row: c, Now: current[c.Sku]))
            .Where(x => x.Row.Cost != x.Now.CostOfGoods)
            .Select(x => new ProductCostChange(x.Row.Sku, x.Now.CostOfGoods, x.Row.Cost, x.Now.Title))
            .OrderBy(c => c.Current is not null)
            .ThenBy(c => c.Sku, StringComparer.Ordinal)
            .ToList();

        LogCostSheetPreviewed(fileName, changes.Count, latest.Count - changes.Count, rejected.Count, marketplaceId);
        return Result.Success(new ProductCostReconciliation(changes, latest.Count - changes.Count, blank, rejected));
    }

    private static decimal? Round(decimal? cost) => cost is { } value ? ProductCostText.Round(value) : null;

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated cost of goods for {Sku} in {MarketplaceId} to {CostOfGoods}")]
    private partial void LogCostUpdated(string sku, string marketplaceId, decimal? costOfGoods);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved cost of goods for {Applied} SKUs in {MarketplaceId} ({Stale} skipped as changed meanwhile)")]
    private partial void LogCostsSaved(int applied, int stale, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Checked cost sheet {FileName}: {Changed} changed, {Unchanged} unchanged, {Rejected} rejected in {MarketplaceId}")]
    private partial void LogCostSheetPreviewed(string fileName, int changed, int unchanged, int rejected, string marketplaceId);
}
