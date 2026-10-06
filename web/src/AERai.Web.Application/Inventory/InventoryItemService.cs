using System.Globalization;
using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// Default <see cref="IInventoryItemService"/>: validates and saves the parts of a SKU that users
/// maintain by hand.
/// </summary>
/// <param name="repository">Item persistence.</param>
/// <param name="images">Picture storage.</param>
/// <param name="spreadsheets">Excel reader for home-stock uploads.</param>
/// <param name="clock">Clock for change timestamps.</param>
/// <param name="logger">Logger.</param>
public sealed partial class InventoryItemService(
    IInventoryItemRepository repository,
    IProductImageStore images,
    ISpreadsheetReader spreadsheets,
    TimeProvider clock,
    ILogger<InventoryItemService> logger) : IInventoryItemService
{
    /// <summary>Largest picture accepted, in bytes (2 MB is plenty for a thumbnail-sized product photo).</summary>
    public const int MaxImageBytes = 2 * 1024 * 1024;

    /// <summary>Sanity ceiling on a home-stock count; anything higher is almost certainly a typo.</summary>
    public const int MaxHomeStock = 1_000_000;

    /// <summary>Most rows accepted in one home-stock upload.</summary>
    public const int MaxHomeStockRows = 10_000;

    /// <summary>Longest lead time or target accepted, in days (two years).</summary>
    public const int MaxLeadTimeDays = 730;

    /// <summary>Required columns of a home-stock upload (normalized header names).</summary>
    public static IReadOnlyList<string> HomeStockColumns { get; } = ["sku", "home-stock"];

    /// <inheritdoc/>
    public Task<InventoryItemDetails?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken) =>
        repository.GetAsync(sku, marketplaceId, cancellationToken);

    /// <inheritdoc/>
    public Task<IReadOnlyList<ProductFamily>> ListFamiliesAsync(CancellationToken cancellationToken) =>
        repository.ListFamiliesAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<Result> UpdateAsync(string sku, string marketplaceId, InventoryItemUpdate update, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(update);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var name = FamilyNames.Normalize(update.Family);
        if (name is not null && FamilyNames.Validate(name) is { } familyError)
        {
            return Result.Failure(familyError);
        }

        if (update.HomeStock is < 0 or > MaxHomeStock)
        {
            return Result.Failure($"Home stock must be between 0 and {MaxHomeStock:N0}.");
        }

        if (ValidateLeadTimes(update.LeadTimes) is { } leadTimeError)
        {
            return Result.Failure(leadTimeError);
        }

        if (!await repository.SaveItemAsync(sku, marketplaceId, name, update.HomeStock, update.LeadTimes, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure($"Product '{sku}' was not found.");
        }

        LogItemUpdated(sku, marketplaceId, name, update.HomeStock);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> SetImageAsync(string sku, Stream content, long length, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentNullException.ThrowIfNull(content);

        if (length is <= 0 or > MaxImageBytes)
        {
            return Result.Failure($"Pictures must be between 1 byte and {MaxImageBytes / (1024 * 1024)} MB.");
        }

        // Buffer (at most 2 MB) so the type can be checked from the bytes themselves; the declared
        // content type and file name are client-controlled and not trusted.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (buffer.Length > MaxImageBytes)
        {
            return Result.Failure($"Pictures can be at most {MaxImageBytes / (1024 * 1024)} MB.");
        }

        if (DetectImageType(buffer.GetBuffer().AsSpan(0, (int)buffer.Length)) is not { } type)
        {
            return Result.Failure("Only JPEG, PNG, or WebP pictures are accepted.");
        }

        var previous = await repository.GetImageAsync(sku, cancellationToken).ConfigureAwait(false);
        var path = $"{Guid.NewGuid():N}{type.Extension}";
        buffer.Position = 0;
        await images.SaveAsync(path, buffer, type.ContentType, cancellationToken).ConfigureAwait(false);

        bool pointed;
        try
        {
            pointed = await repository.SetImageAsync(sku, path, type.ContentType, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // The product still points at its old picture; don't leave the new blob behind.
            await images.DeleteAsync(path, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        if (!pointed)
        {
            await images.DeleteAsync(path, cancellationToken).ConfigureAwait(false);
            return Result.Failure($"Product '{sku}' was not found.");
        }

        // Only after the product points at the new picture, so a failure never leaves it without one.
        if (previous is { } old)
        {
            await images.DeleteAsync(old.Path, cancellationToken).ConfigureAwait(false);
        }

        LogImageSet(sku, path);
        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<Result> RemoveImageAsync(string sku, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var previous = await repository.GetImageAsync(sku, cancellationToken).ConfigureAwait(false);
        if (!await repository.SetImageAsync(sku, null, null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure($"Product '{sku}' was not found.");
        }

        if (previous is { } old)
        {
            await images.DeleteAsync(old.Path, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    /// <inheritdoc/>
    public async Task<ProductImage?> OpenImageAsync(string sku, CancellationToken cancellationToken)
    {
        if (await repository.GetImageAsync(sku, cancellationToken).ConfigureAwait(false) is not { } image)
        {
            return null;
        }

        var stream = await images.OpenReadAsync(image.Path, cancellationToken).ConfigureAwait(false);
        return stream is null ? null : new ProductImage(stream, image.ContentType);
    }

    /// <inheritdoc/>
    public async Task<Result<int>> SetHomeStockAsync(string marketplaceId, IReadOnlyList<HomeStockEntry> entries, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        if (entries.FirstOrDefault(e => e.Quantity is < 0 or > MaxHomeStock) is { } bad)
        {
            return Result.Failure<int>($"Home stock for {bad.Sku} must be between 0 and {MaxHomeStock:N0}.");
        }

        var existing = await repository.GetExistingSkusAsync(entries.Select(e => e.Sku).ToList(), cancellationToken).ConfigureAwait(false);
        if (entries.FirstOrDefault(e => !existing.Contains(e.Sku)) is { } unknown)
        {
            return Result.Failure<int>($"Product '{unknown.Sku}' was not found.");
        }

        // The same SKU twice in one save keeps the last value, like a spreadsheet read top to bottom.
        var unique = entries.GroupBy(e => e.Sku, StringComparer.Ordinal).Select(g => g.Last()).ToList();
        await repository.SetHomeStockAsync(marketplaceId, unique, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);

        LogHomeStockSaved(unique.Count, marketplaceId);
        return Result.Success(unique.Count);
    }

    /// <inheritdoc/>
    public async Task<Result<HomeStockImportResult>> ImportHomeStockAsync(string marketplaceId, string fileName, Stream content, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var parsed = Path.GetExtension(fileName).ToLowerInvariant() switch
        {
            ".xlsx" => await spreadsheets.ReadAsync(content, MaxHomeStockRows, cancellationToken).ConfigureAwait(false),
            ".csv" or ".tsv" or ".txt" => await ParseTextAsync(content, cancellationToken).ConfigureAwait(false),
            _ => Result.Failure<ParsedFile>("Upload a .xlsx, .csv, or .tsv file."),
        };
        if (parsed.IsFailure)
        {
            return Result.Failure<HomeStockImportResult>(parsed.Error);
        }

        if (HomeStockColumns.FirstOrDefault(c => !parsed.Value.Headers.Contains(c, StringComparer.Ordinal)) is { } missing)
        {
            return Result.Failure<HomeStockImportResult>($"The file has no '{missing}' column. Use the template: columns sku and home-stock.");
        }

        var records = parsed.Value.Records;
        var existing = await repository.GetExistingSkusAsync(
            records.Select(r => r.Get("sku")).OfType<string>().Distinct(StringComparer.Ordinal).ToList(), cancellationToken).ConfigureAwait(false);

        var rejected = new List<RejectedRow>();
        var valid = new List<HomeStockEntry>();
        foreach (var record in records)
        {
            var sku = record.Get("sku");
            var quantityText = record.Get("home-stock");
            if (sku is null)
            {
                rejected.Add(new RejectedRow(record.RowNumber, "Missing sku.", record.RawLine));
            }
            else if (!existing.Contains(sku))
            {
                rejected.Add(new RejectedRow(record.RowNumber, $"Unknown SKU '{sku}'.", record.RawLine));
            }
            else if (!int.TryParse(quantityText, NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var quantity) || quantity is < 0 or > MaxHomeStock)
            {
                rejected.Add(new RejectedRow(record.RowNumber, $"home-stock must be a whole number from 0 to {MaxHomeStock:N0} (got '{quantityText}').", record.RawLine));
            }
            else
            {
                valid.Add(new HomeStockEntry(sku, quantity));
            }
        }

        var unique = valid.GroupBy(e => e.Sku, StringComparer.Ordinal).Select(g => g.Last()).ToList();
        if (unique.Count > 0)
        {
            await repository.SetHomeStockAsync(marketplaceId, unique, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
        }

        LogHomeStockImported(fileName, unique.Count, rejected.Count, marketplaceId);
        return Result.Success(new HomeStockImportResult(unique.Count, rejected));
    }

    /// <summary>Checks that every set lead-time field is in range; the target must also be at least a day.</summary>
    private static string? ValidateLeadTimes(LeadTimeSettings settings)
    {
        (string Name, int? Value, int Min)[] fields =
        [
            ("Supplier lead time", settings.SupplierLeadTimeDays, 0),
            ("Prep time", settings.PrepTimeDays, 0),
            ("Transit time", settings.TransitDays, 0),
            ("Safety stock", settings.SafetyStockDays, 0),
            ("Target cover", settings.TargetStockDays, 1),
        ];

        return fields.FirstOrDefault(f => f.Value is { } v && (v < f.Min || v > MaxLeadTimeDays)) is { Name: { } bad } field
            ? $"{bad} must be between {field.Min} and {MaxLeadTimeDays} days."
            : null;
    }

    private static async Task<Result<ParsedFile>> ParseTextAsync(Stream content, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return await DelimitedTextParser.ParseAsync(reader, MaxHomeStockRows, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Identifies JPEG, PNG, or WebP from the file's leading bytes (its signature).</summary>
    private static (string ContentType, string Extension)? DetectImageType(ReadOnlySpan<byte> bytes)
    {
        if (bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return ("image/jpeg", ".jpg");
        }

        if (bytes.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return ("image/png", ".png");
        }

        // RIFF container whose form type (bytes 8-11) is WEBP.
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WEBP"u8))
        {
            return ("image/webp", ".webp");
        }

        return null;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Updated {Sku} in {MarketplaceId}: family {Family}, home stock {HomeStock}")]
    private partial void LogItemUpdated(string sku, string marketplaceId, string? family, int homeStock);

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored a new picture for {Sku} at {ImagePath}")]
    private partial void LogImageSet(string sku, string imagePath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Saved home stock for {Count} SKUs in {MarketplaceId}")]
    private partial void LogHomeStockSaved(int count, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Imported home stock from {FileName}: {Saved} saved, {Rejected} rejected in {MarketplaceId}")]
    private partial void LogHomeStockImported(string fileName, int saved, int rejected, string marketplaceId);
}
