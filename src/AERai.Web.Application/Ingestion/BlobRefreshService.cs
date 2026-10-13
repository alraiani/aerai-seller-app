using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Ingestion;

/// <summary>Default <see cref="IBlobRefreshService"/>.</summary>
/// <param name="rawFiles">The raw landing zone.</param>
/// <param name="staging">Stages a report file from raw storage.</param>
/// <param name="promotion">Promotes a staged batch into the reports.</param>
/// <param name="orders">Writes Orders API orders.</param>
/// <param name="applied">Which files are already loaded.</param>
/// <param name="marketplaces">Marketplace lookups.</param>
/// <param name="clock">Clock for the ledger timestamp.</param>
/// <param name="logger">Logger.</param>
public sealed partial class BlobRefreshService(
    IRawFileStore rawFiles,
    IStagingImportService staging,
    IPromotionService promotion,
    IAmazonOrderRepository orders,
    IAppliedBlobRepository applied,
    IMarketplaceQueries marketplaces,
    TimeProvider clock,
    ILogger<BlobRefreshService> logger) : IBlobRefreshService
{
    /// <summary>Most files one refresh loads; refresh again for the rest.</summary>
    public const int MaxFilesPerRefresh = 500;

    /// <summary>Orders written per database round trip, so a large page never builds one huge transaction.</summary>
    private const int OrdersPerWrite = 500;

    /// <summary>Raw folder for Orders API pages (written by <see cref="ReportIngestionService"/>).</summary>
    private const string OrdersApiFolder = ReportIngestionService.OrdersApiRawFolder;

    /// <summary>Metadata the app writes on files it saved itself; a hand-uploaded file has none.</summary>
    private const string AppWrittenMetadataKey = "syncrunid";

    private const string MarketplaceMetadataKey = "marketplaceid";

    /// <summary>Folders holding reports that are dated by the day they arrive; never loaded here.</summary>
    private static readonly HashSet<string> SnapshotFolders = new(StringComparer.Ordinal)
    {
        "fbainventory", "fbareservedinventory", "fbarestockrecommendations", "awdinventory",
    };

    /// <summary>Raw folders holding report files, and the import each is staged as.</summary>
    private static readonly Dictionary<string, ImportSource> ReportFolders = new(StringComparer.Ordinal)
    {
        ["orders"] = ImportSource.Orders,
        ["settlements"] = ImportSource.Settlements,
        ["inventory"] = ImportSource.Inventory,
    };

    /// <inheritdoc/>
    public async Task<BlobRefreshPreview> PreviewAsync(BlobRefreshRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var plan = await PlanAsync(request, cancellationToken).ConfigureAwait(false);
        return new BlobRefreshPreview(
            plan.Pending.Count(f => f.Kind == FileKind.Report),
            plan.Pending.Count(f => f.Kind == FileKind.OrdersApiPage),
            plan.AlreadyLoaded,
            plan.SnapshotFiles);
    }

    /// <inheritdoc/>
    public async Task<Result<BlobRefreshSummary>> RefreshAsync(BlobRefreshRequest request, string user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var all = await marketplaces.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var byId = all.ToDictionary(m => m.MarketplaceId, StringComparer.Ordinal);
        if (!byId.TryGetValue(request.DefaultMarketplaceId, out var fallback) || !fallback.IsActive)
        {
            return Result.Failure<BlobRefreshSummary>("Choose an active marketplace for files that do not say which one they belong to.");
        }

        var plan = await PlanAsync(request, cancellationToken).ConfigureAwait(false);
        var hitLimit = plan.Pending.Count > MaxFilesPerRefresh;

        int loaded = 0, empty = 0, ordersWritten = 0, promoted = 0;
        var problems = new List<string>();
        foreach (var file in plan.Pending.Take(MaxFilesPerRefresh))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var marketplaceId = file.Info.Metadata.TryGetValue(MarketplaceMetadataKey, out var named) && !string.IsNullOrWhiteSpace(named)
                ? named
                : request.DefaultMarketplaceId;
            if (!byId.TryGetValue(marketplaceId, out var marketplace) || !marketplace.IsActive)
            {
                problems.Add($"{file.Info.Path}: marketplace '{marketplaceId}' does not exist or is not active.");
                continue;
            }

            // A settlement file goes to the marketplace that keeps that marketplace's payouts (Canada's
            // are listed with the United States), the same as a settlement pulled from Amazon.
            if (file.Source == ImportSource.Settlements && marketplace.SettlementMarketplaceId is { } settlementMarketplaceId)
            {
                marketplaceId = settlementMarketplaceId;
            }

            try
            {
                var outcome = file.Kind == FileKind.Report
                    ? await LoadReportAsync(file, marketplaceId, request.Promote, user, cancellationToken).ConfigureAwait(false)
                    : await LoadOrdersPageAsync(file, marketplace, user, cancellationToken).ConfigureAwait(false);

                loaded += outcome.Loaded ? 1 : 0;
                empty += outcome.Empty ? 1 : 0;
                ordersWritten += outcome.OrdersWritten;
                promoted += outcome.Promoted ? 1 : 0;
                if (outcome.Problem is not null)
                {
                    problems.Add($"{file.Info.Path}: {outcome.Problem}");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // One bad file must be reported, not stop the files after it.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogFileFailed(ex, file.Info.Path);
                problems.Add($"{file.Info.Path}: {ex.Message}");
            }
        }

        LogRefreshed(loaded, empty, problems.Count, user);
        return Result.Success(new BlobRefreshSummary(loaded, empty, plan.AlreadyLoaded, plan.SnapshotFiles, ordersWritten, promoted, problems, hitLimit));
    }

    /// <summary>Lists the raw container once and sorts every file into pending, already loaded, snapshot, or ignored.</summary>
    private async Task<Plan> PlanAsync(BlobRefreshRequest request, CancellationToken cancellationToken)
    {
        var files = await rawFiles.ListAsync(string.Empty, cancellationToken).ConfigureAwait(false);
        var loaded = request.Reprocess
            ? new HashSet<string>(StringComparer.Ordinal)
            : await applied.GetLoadedPathsAsync(cancellationToken).ConfigureAwait(false);

        var pending = new List<PendingFile>();
        var alreadyLoaded = 0;
        var snapshots = 0;
        foreach (var info in files)
        {
            var folder = FolderOf(info.Path);
            if (SnapshotFolders.Contains(folder))
            {
                snapshots++;
                continue;
            }

            FileKind kind;
            ImportSource? source = null;
            if (ReportFolders.TryGetValue(folder, out var reportSource))
            {
                kind = FileKind.Report;
                source = reportSource;
            }
            else if (folder == OrdersApiFolder)
            {
                kind = FileKind.OrdersApiPage;
            }
            else
            {
                continue; // Not one of ours (for example a stray upload): leave it alone.
            }

            // A page the app saved itself was applied the moment it arrived, so only a hand-uploaded page is new.
            var appliedByApp = kind == FileKind.OrdersApiPage && info.Metadata.ContainsKey(AppWrittenMetadataKey) && !request.Reprocess;
            if (loaded.Contains(info.Path) || appliedByApp)
            {
                alreadyLoaded++;
                continue;
            }

            pending.Add(new PendingFile(info, kind, source));
        }

        return new Plan(pending, alreadyLoaded, snapshots);
    }

    private async Task<FileOutcome> LoadReportAsync(PendingFile file, string marketplaceId, bool promote, string user, CancellationToken cancellationToken)
    {
        string sha256;
        var content = await rawFiles.OpenReadAsync(file.Info.Path, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return FileOutcome.Failed("the file disappeared before it could be read.");
        }

        await using (content.ConfigureAwait(false))
        {
            sha256 = Convert.ToHexStringLower(await SHA256.HashDataAsync(content, cancellationToken).ConfigureAwait(false));
        }

        var staged = await staging.StageRawFileAsync(
            new StageRawFileCommand(file.Source!.Value, marketplaceId, file.Info.Path, sha256, DisplayNameOf(file.Info.Path), user), // Non-null: reports always have a source.
            cancellationToken).ConfigureAwait(false);
        if (staged.IsFailure)
        {
            if (staged.Error == StagingImportService.NoDataRowsError)
            {
                await MarkAppliedAsync(file, "empty file", user, cancellationToken).ConfigureAwait(false);
                return FileOutcome.EmptyFile;
            }

            return FileOutcome.Failed(staged.Error);
        }

        if (!promote)
        {
            return FileOutcome.StagedOnly;
        }

        var result = await promotion.PromoteAsync(staged.Value.BatchId, cancellationToken).ConfigureAwait(false);
        return result.IsSuccess
            ? FileOutcome.StagedAndPromoted
            : FileOutcome.StagedButNotPromoted($"staged as batch {staged.Value.BatchId} but promotion failed: {result.Error}");
    }

    private async Task<FileOutcome> LoadOrdersPageAsync(PendingFile file, Marketplace marketplace, string user, CancellationToken cancellationToken)
    {
        var content = await rawFiles.OpenReadAsync(file.Info.Path, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return FileOutcome.Failed("the file disappeared before it could be read.");
        }

        string json;
        await using (content.ConfigureAwait(false))
        {
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        AmazonOrdersPage page;
        try
        {
            page = OrdersPageParser.Parse(json, marketplace.Currency);
        }
        catch (JsonException ex)
        {
            return FileOutcome.Failed($"not a valid Orders API response ({ex.Message}).");
        }

        var written = 0;
        var distinct = page.Orders
            .GroupBy(o => o.AmazonOrderId, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(o => o.LastUpdatedAt).First())
            .ToList();
        foreach (var chunk in distinct.Chunk(OrdersPerWrite))
        {
            var result = await orders.UpsertAsync(marketplace.MarketplaceId, chunk, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
            written += result.Total;
        }

        await MarkAppliedAsync(file, written == 1 ? "1 order" : $"{written} orders", user, cancellationToken).ConfigureAwait(false);
        return FileOutcome.OrdersLoaded(written);
    }

    private Task MarkAppliedAsync(PendingFile file, string outcome, string user, CancellationToken cancellationToken) =>
        applied.MarkAppliedAsync(
            new AppliedBlob { Path = file.Info.Path, Source = FolderOf(file.Info.Path), Outcome = outcome, AppliedAt = clock.GetUtcNow(), AppliedBy = user },
            cancellationToken);

    private static string FolderOf(string path)
    {
        var slash = path.IndexOf('/', StringComparison.Ordinal);
        return slash < 0 ? string.Empty : path[..slash];
    }

    /// <summary>The file name without the <c>{guid}-</c> the app puts in front of it, for the batch list.</summary>
    private static string DisplayNameOf(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return name.Length > 33 && name[32] == '-' && name[..32].All(Uri.IsHexDigit) ? name[33..] : name;
    }

    private enum FileKind
    {
        Report,
        OrdersApiPage,
    }

    private sealed record PendingFile(RawFileInfo Info, FileKind Kind, ImportSource? Source);

    private sealed record Plan(IReadOnlyList<PendingFile> Pending, int AlreadyLoaded, int SnapshotFiles);

    private sealed record FileOutcome(bool Loaded, bool Empty, bool Promoted, int OrdersWritten, string? Problem)
    {
        public static readonly FileOutcome EmptyFile = new(false, true, false, 0, null);

        public static readonly FileOutcome StagedOnly = new(true, false, false, 0, null);

        public static readonly FileOutcome StagedAndPromoted = new(true, false, true, 0, null);

        public static FileOutcome Failed(string problem) => new(false, false, false, 0, problem);

        public static FileOutcome StagedButNotPromoted(string problem) => new(true, false, false, 0, problem);

        public static FileOutcome OrdersLoaded(int written) => new(true, false, false, written, null);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Blob refresh loaded {Loaded} files ({Empty} empty, {Problems} problems) for {User}")]
    private partial void LogRefreshed(int loaded, int empty, int problems, string user);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Blob refresh could not load {Path}")]
    private partial void LogFileFailed(Exception exception, string path);
}
