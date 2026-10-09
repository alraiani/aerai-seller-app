using System.IO.Compression;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// Default <see cref="IProductPictureService"/>. Every picture, from Amazon or an upload, is stored
/// through <see cref="IInventoryItemService.SetImageAsync"/>, so the signature check, size limit, and
/// old-blob cleanup are the same as for a single upload on the Edit page.
/// </summary>
/// <param name="repository">Item persistence (SKUs, ASINs, current pictures).</param>
/// <param name="items">Stores each picture.</param>
/// <param name="catalog">Amazon's catalog.</param>
/// <param name="connection">Whether Amazon can be called.</param>
/// <param name="logger">Logger.</param>
public sealed partial class ProductPictureService(
    IInventoryItemRepository repository,
    IInventoryItemService items,
    IAmazonCatalogGateway catalog,
    IAmazonConnectionInfo connection,
    ILogger<ProductPictureService> logger) : IProductPictureService
{
    /// <summary>
    /// Most listing pictures downloaded per Amazon pull, so one request stays well inside a web
    /// request's time limit; SKUs that got a picture drop out, so the next pull carries on.
    /// </summary>
    public const int MaxAmazonDownloadsPerPull = 200;

    /// <summary>Most pictures accepted in one upload, counting every picture inside .zip files.</summary>
    public const int MaxFilesPerUpload = 500;

    /// <summary>Picture file extensions a bulk upload considers.</summary>
    public static IReadOnlyList<string> PictureExtensions { get; } = [".jpg", ".jpeg", ".png", ".webp"];

    // Amazon's picture CDN is not SP-API rate-limited; a few at a time is quick without hammering it.
    private const int DownloadConcurrency = 4;

    /// <inheritdoc/>
    public async Task<PictureCoverage> GetCoverageAsync(CancellationToken cancellationToken)
    {
        var targets = await repository.ListPictureTargetsAsync(cancellationToken).ConfigureAwait(false);
        return new PictureCoverage(
            targets.Count,
            targets.Count(t => t.HasImage),
            targets.Count(t => !t.HasImage && t.Asin is not null),
            targets.Count(t => !t.HasImage && t.Asin is null));
    }

    /// <inheritdoc/>
    public async Task<Result<PictureImportResult>> PullFromAmazonAsync(Marketplace marketplace, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        if (!connection.CanRun)
        {
            return Result.Failure<PictureImportResult>($"Amazon isn't connected: {connection.Problem}");
        }

        var missing = (await repository.ListPictureTargetsAsync(cancellationToken).ConfigureAwait(false)).Where(t => !t.HasImage).ToList();
        var withoutAsin = missing.Count(t => t.Asin is null);

        // One download per ASIN, even when several SKUs (e.g. FBA and merchant-fulfilled) share it.
        var skusByAsin = missing
            .Where(t => t.Asin is not null)
            .GroupBy(t => t.Asin!.Trim().ToUpperInvariant(), StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(t => t.Sku).ToList(), StringComparer.Ordinal);
        if (skusByAsin.Count == 0)
        {
            return Result.Success(new PictureImportResult([], 0, withoutAsin));
        }

        IReadOnlyDictionary<string, Uri> found;
        try
        {
            found = await catalog.FindMainImagesAsync(marketplace, skusByAsin.Keys, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException)
        {
            LogCatalogFailed(ex, marketplace.MarketplaceId);
            return Result.Failure<PictureImportResult>($"Amazon's catalog couldn't be read: {ex.Message}");
        }

        var outcomes = skusByAsin
            .Where(pair => !found.ContainsKey(pair.Key))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new PictureImportOutcome(pair.Key, pair.Value, PictureImportStatus.NotOnAmazon, "Amazon's catalog has no picture for this ASIN."))
            .ToList();

        var queue = found.Where(pair => skusByAsin.ContainsKey(pair.Key)).OrderBy(pair => pair.Key, StringComparer.Ordinal).ToList();
        var thisPull = queue.Take(MaxAmazonDownloadsPerPull).ToList();
        var remaining = queue.Skip(MaxAmazonDownloadsPerPull).Sum(pair => skusByAsin[pair.Key].Count);

        foreach (var batch in thisPull.Chunk(DownloadConcurrency))
        {
            // Downloads run in parallel; storing stays sequential because it shares one database context.
            var downloads = await Task.WhenAll(batch.Select(pair => DownloadAsync(pair.Key, pair.Value, cancellationToken))).ConfigureAwait(false);
            foreach (var (asin, bytes, error) in downloads)
            {
                if (bytes is null)
                {
                    outcomes.Add(new PictureImportOutcome(asin, skusByAsin[asin], PictureImportStatus.Failed, error));
                    continue;
                }

                outcomes.AddRange(await StoreAmazonPictureAsync(asin, skusByAsin[asin], bytes, cancellationToken).ConfigureAwait(false));
            }
        }

        var result = new PictureImportResult(outcomes, remaining, withoutAsin);
        var (stored, notOnAmazon, failed) = (result.StoredSkus, result.Count(PictureImportStatus.NotOnAmazon), result.Count(PictureImportStatus.Failed));
        LogPulled(marketplace.MarketplaceId, stored, notOnAmazon, failed, remaining);
        return Result.Success(result);
    }

    /// <inheritdoc/>
    public async Task<Result<PictureImportResult>> UploadAsync(IReadOnlyList<PictureFile> files, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(files);

        if (files.Count == 0)
        {
            return Result.Failure<PictureImportResult>("Choose at least one picture or .zip file.");
        }

        var archives = new List<ZipArchive>();
        try
        {
            var outcomes = new List<PictureImportOutcome>();
            var candidates = new List<(string Source, string Name, Func<Stream> Open, bool InArchive)>();
            foreach (var file in files)
            {
                var fileName = LastSegment(file.FileName);
                if (!string.Equals(Path.GetExtension(fileName), ".zip", StringComparison.OrdinalIgnoreCase))
                {
                    candidates.Add((fileName, fileName, () => file.Content, false));
                    continue;
                }

                ZipArchive archive;
                try
                {
                    archive = new ZipArchive(file.Content, ZipArchiveMode.Read, leaveOpen: true);
                }
                catch (InvalidDataException)
                {
                    outcomes.Add(new PictureImportOutcome(fileName, [], PictureImportStatus.Rejected, "Not a valid .zip file."));
                    continue;
                }

                archives.Add(archive);
                foreach (var entry in archive.Entries.Where(e => !IsArchiveClutter(e.FullName)))
                {
                    var entryName = LastSegment(entry.FullName);
                    candidates.Add(($"{fileName} › {entry.FullName}", entryName, entry.Open, true));
                }
            }

            if (candidates.Count > MaxFilesPerUpload)
            {
                return Result.Failure<PictureImportResult>($"Upload at most {MaxFilesPerUpload:N0} pictures at a time (this upload has {candidates.Count:N0}).");
            }

            var matcher = new PictureMatcher(await repository.ListPictureTargetsAsync(cancellationToken).ConfigureAwait(false));
            var claimed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (source, name, open, inArchive) in candidates)
            {
                outcomes.Add(await StoreUploadAsync(source, name, open, inArchive, matcher, claimed, cancellationToken).ConfigureAwait(false));
            }

            var result = new PictureImportResult(outcomes);
            var (stored, noMatch, rejected) = (result.StoredSkus, result.Count(PictureImportStatus.NoMatch), result.Count(PictureImportStatus.Rejected));
            LogUploaded(candidates.Count, stored, noMatch, rejected);
            return Result.Success(result);
        }
        finally
        {
            foreach (var archive in archives)
            {
                archive.Dispose();
            }
        }
    }

    private async Task<PictureImportOutcome> StoreUploadAsync(
        string source, string name, Func<Stream> open, bool inArchive, PictureMatcher matcher, HashSet<string> claimed, CancellationToken cancellationToken)
    {
        if (!PictureExtensions.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase))
        {
            return new PictureImportOutcome(source, [], PictureImportStatus.Rejected, "Not a .jpg, .png, or .webp file.");
        }

        var (targets, problem) = matcher.Match(Path.GetFileNameWithoutExtension(name).Trim());
        if (targets.Count == 0)
        {
            return new PictureImportOutcome(source, [], PictureImportStatus.NoMatch, problem);
        }

        var unclaimed = targets.Where(t => claimed.Add(t.Sku)).ToList();
        if (unclaimed.Count == 0)
        {
            return new PictureImportOutcome(source, targets.Select(t => t.Sku).ToList(), PictureImportStatus.Duplicate, "An earlier file in this upload already set this picture.");
        }

        byte[]? bytes;
        var stream = open();
        try
        {
            bytes = await ImageFiles.ReadAtMostAsync(stream, InventoryItemService.MaxImageBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return new PictureImportOutcome(source, [], PictureImportStatus.Rejected, "The file is damaged inside the .zip.");
        }
        finally
        {
            // Zip entry streams are ours to close; an uploaded file's stream belongs to the caller.
            if (inArchive)
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }

        if (bytes is null)
        {
            return new PictureImportOutcome(source, [], PictureImportStatus.Rejected, $"Larger than {InventoryItemService.MaxImageBytes / (1024 * 1024)} MB.");
        }

        if (bytes.Length == 0 || ImageFiles.DetectType(bytes) is null)
        {
            return new PictureImportOutcome(source, [], PictureImportStatus.Rejected, "Not a JPEG, PNG, or WebP picture.");
        }

        var stored = new List<string>();
        foreach (var target in unclaimed)
        {
            using var content = new MemoryStream(bytes, writable: false);
            var result = await items.SetImageAsync(target.Sku, content, bytes.Length, cancellationToken).ConfigureAwait(false);
            if (result.IsFailure)
            {
                return new PictureImportOutcome(source, [target.Sku], PictureImportStatus.Failed, result.Error);
            }

            stored.Add(target.Sku);
        }

        return new PictureImportOutcome(source, stored, unclaimed.Any(t => t.HasImage) ? PictureImportStatus.Replaced : PictureImportStatus.Added);
    }

    private async Task<IEnumerable<PictureImportOutcome>> StoreAmazonPictureAsync(string asin, IReadOnlyList<string> skus, byte[] bytes, CancellationToken cancellationToken)
    {
        var stored = new List<string>();
        var outcomes = new List<PictureImportOutcome>();
        foreach (var sku in skus)
        {
            // Someone may have uploaded a picture since the pull started; theirs wins.
            if (await repository.GetImageAsync(sku, cancellationToken).ConfigureAwait(false) is not null)
            {
                continue;
            }

            using var content = new MemoryStream(bytes, writable: false);
            var result = await items.SetImageAsync(sku, content, bytes.Length, cancellationToken).ConfigureAwait(false);
            if (result.IsSuccess)
            {
                stored.Add(sku);
            }
            else
            {
                outcomes.Add(new PictureImportOutcome(asin, [sku], PictureImportStatus.Failed, result.Error));
            }
        }

        if (stored.Count > 0)
        {
            outcomes.Insert(0, new PictureImportOutcome(asin, stored, PictureImportStatus.Added));
        }

        return outcomes;
    }

    private async Task<(string Asin, byte[]? Bytes, string? Error)> DownloadAsync(string asin, Uri url, CancellationToken cancellationToken)
    {
        try
        {
            var stream = await catalog.OpenImageAsync(url, cancellationToken).ConfigureAwait(false);
            await using (stream.ConfigureAwait(false))
            {
                var bytes = await ImageFiles.ReadAtMostAsync(stream, InventoryItemService.MaxImageBytes, cancellationToken).ConfigureAwait(false);
                return bytes is null
                    ? (asin, null, $"Amazon's picture is larger than {InventoryItemService.MaxImageBytes / (1024 * 1024)} MB.")
                    : (asin, bytes, null);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException
                                   || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // One bad picture must not stop the rest of the pull.
            LogDownloadFailed(ex, asin);
            return (asin, null, ex is TaskCanceledException ? "The download timed out." : $"The download failed: {ex.Message}");
        }
    }

    /// <summary>Folder entries and the metadata files macOS and Windows put into archives.</summary>
    private static bool IsArchiveClutter(string fullName)
    {
        var name = LastSegment(fullName);
        return name.Length == 0
            || name.StartsWith('.')
            || fullName.Replace('\\', '/').StartsWith("__MACOSX/", StringComparison.Ordinal)
            || string.Equals(name, "Thumbs.db", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The file name without any folder, whichever separator the browser or zip tool used.</summary>
    private static string LastSegment(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized[(normalized.LastIndexOf('/') + 1)..];
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Pulled Amazon pictures in {MarketplaceId}: {Stored} SKUs stored, {NotOnAmazon} ASINs without a picture, {Failed} failed, {Remaining} SKUs left")]
    private partial void LogPulled(string marketplaceId, int stored, int notOnAmazon, int failed, int remaining);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Amazon catalog lookup failed in {MarketplaceId}")]
    private partial void LogCatalogFailed(Exception exception, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Downloading Amazon's picture for {Asin} failed")]
    private partial void LogDownloadFailed(Exception exception, string asin);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bulk picture upload: {Files} files, {Stored} SKUs stored, {NoMatch} unmatched, {Rejected} rejected")]
    private partial void LogUploaded(int files, int stored, int noMatch, int rejected);
}
