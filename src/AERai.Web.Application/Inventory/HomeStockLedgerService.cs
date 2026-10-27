using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Inventory;

/// <summary>Default <see cref="IHomeStockLedgerService"/>.</summary>
/// <remarks>
/// Every send to Amazon, whichever page logs it, stores the SKU's FBA stock at that moment (its
/// <see cref="AmazonBaseline"/>) so <see cref="PendingReconciliation"/> can tell when Amazon shows the units,
/// and queues an inventory sync so that happens soon.
/// </remarks>
/// <param name="repository">Ledger persistence.</param>
/// <param name="inventory">Inventory reads (each SKU's FBA stock when a send is logged).</param>
/// <param name="refresh">Queues an inventory sync after a send to Amazon.</param>
/// <param name="alerts">Asks for a stock-alert refresh after home stock changes (it drives reorder timing).</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class HomeStockLedgerService(
    IHomeStockLedgerRepository repository,
    IInventoryQueries inventory,
    IInventoryRefreshRequester refresh,
    IStockAlertRefreshSignal alerts,
    TimeProvider clock,
    ILogger<HomeStockLedgerService> logger) : IHomeStockLedgerService
{
    /// <summary>Longest reference accepted.</summary>
    public const int MaxReferenceLength = 100;

    /// <summary>Longest note accepted.</summary>
    public const int MaxNoteLength = 500;

    /// <summary>
    /// Most entries one action of the ledger's grouped view lists; the page links to the full list for the rest.
    /// </summary>
    public const int MaxGroupRows = 250;

    /// <summary>Most entries one bulk reason change may touch (a full action of the grouped view).</summary>
    public const int MaxBulkEntries = MaxGroupRows;

    private static readonly IReadOnlyDictionary<string, AmazonBaseline> NoBaselines = new Dictionary<string, AmazonBaseline>();

    /// <summary>How far ahead a movement may be dated (clock skew between browser and server).</summary>
    private static readonly TimeSpan FutureTolerance = TimeSpan.FromDays(1);

    /// <inheritdoc/>
    public async Task<Result<string>> RecordAsync(string marketplaceId, HomeStockMovementInput input, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var now = clock.GetUtcNow();
        var sku = input.Sku?.Trim() ?? string.Empty;
        var reference = Tidy(input.Reference);
        var note = Tidy(input.Note);

        if (Validate(input, sku, reference, note, now) is { } error)
        {
            return Result.Failure<string>(error);
        }

        var units = input.Type switch
        {
            HomeStockMovementType.ShippedToAmazon => -input.Quantity,
            _ => input.Quantity,
        };

        var isSend = input.Type == HomeStockMovementType.ShippedToAmazon;
        var baseline = isSend ? (await BaselinesAsync(marketplaceId, cancellationToken).ConfigureAwait(false)).GetValueOrDefault(sku) ?? AmazonBaseline.Of(null) : null;
        var write = new HomeStockLedgerWrite(marketplaceId, sku, input.Type, units, input.OccurredAt ?? now, reference, note, null, now, user, baseline);
        var (outcome, id) = await repository.RecordAsync(write, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case HomeStockLedgerOutcome.Recorded:
                LogRecorded(id, input.Type, sku, units, marketplaceId);
                alerts.Request();
                if (isSend)
                {
                    await refresh.RequestAsync(marketplaceId, user, cancellationToken).ConfigureAwait(false);
                }

                return Result.Success(Describe(input, sku));
            case HomeStockLedgerOutcome.Unchanged:
                return Result.Success($"{sku} already has {input.Quantity:N0} at home; nothing to record.");
            case HomeStockLedgerOutcome.NotFound:
                return Result.Failure<string>($"There is no SKU \"{sku}\".");
            case HomeStockLedgerOutcome.WouldGoNegative:
                return Result.Failure<string>($"That would take {sku}'s home stock below zero. Check the quantity, or log a count correction first.");
            default:
                return Result.Failure<string>("The movement could not be recorded.");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<HomeStockCountResult>> ApplyCountsAsync(string marketplaceId, IReadOnlyList<HomeStockCountChange> changes, HomeStockCountOptions options, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(changes);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var now = clock.GetUtcNow();
        var reference = Tidy(options.Reference);
        var note = Tidy(options.Note);
        var wanted = changes.Where(c => c.Difference != 0).ToList();
        var hasIncreases = wanted.Any(c => c.Difference > 0);
        var hasDecreases = wanted.Any(c => c.Difference < 0);

        string? error =
            wanted.Count == 0 ? "There are no changes to apply."
            : wanted.Count > InventoryItemService.MaxHomeStockRows ? $"Apply at most {InventoryItemService.MaxHomeStockRows:N0} changes at a time."
            : wanted.Any(c => c.Current is < 0 or > InventoryItemService.MaxHomeStock || c.New is < 0 or > InventoryItemService.MaxHomeStock)
                ? $"Counts must be between 0 and {InventoryItemService.MaxHomeStock:N0}."
            : hasIncreases && !HomeStockCountOptions.IncreaseTypes.Contains(options.IncreaseType) ? "Choose how to log the increases."
            : hasDecreases && !HomeStockCountOptions.DecreaseTypes.Contains(options.DecreaseType) ? "Choose how to log the decreases."
            : note is null && ((hasIncreases && options.IncreaseType == HomeStockMovementType.Other) || (hasDecreases && options.DecreaseType == HomeStockMovementType.Other))
                ? "Add a note saying what these movements were (required for Other)."
            : reference?.Length > MaxReferenceLength ? $"The reference can be at most {MaxReferenceLength} characters."
            : note?.Length > MaxNoteLength ? $"The note can be at most {MaxNoteLength} characters."
            : options.OccurredAt > now + FutureTolerance ? "The date can't be in the future."
            : null;
        if (error is not null)
        {
            return Result.Failure<HomeStockCountResult>(error);
        }

        // The same SKU twice keeps the last line, matching how the sheet was reviewed.
        var unique = wanted.GroupBy(c => c.Sku, StringComparer.Ordinal).Select(g => g.Last()).ToList();
        var template = new HomeStockLedgerWrite(marketplaceId, string.Empty, HomeStockMovementType.CountCorrection, 0, options.OccurredAt ?? now, reference, note, null, now, user);
        var sends = hasDecreases && options.DecreaseType == HomeStockMovementType.ShippedToAmazon;
        var baselines = sends ? await BaselinesAsync(marketplaceId, cancellationToken).ConfigureAwait(false) : NoBaselines;
        var (applied, stale) = await repository.ApplyCountsAsync(marketplaceId, unique, options.IncreaseType, options.DecreaseType, template, baselines, cancellationToken).ConfigureAwait(false);

        var unitsIn = applied.Where(c => c.Difference > 0).Sum(c => c.Difference);
        var unitsOut = -applied.Where(c => c.Difference < 0).Sum(c => c.Difference);
        LogCountsApplied(applied.Count, unitsIn, unitsOut, stale.Count, marketplaceId);
        if (applied.Count > 0)
        {
            alerts.Request();
        }

        if (sends && unitsOut > 0)
        {
            await refresh.RequestAsync(marketplaceId, user, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new HomeStockCountResult(applied.Count, unitsIn, unitsOut, stale));
    }

    /// <inheritdoc/>
    public async Task<Result<HomeStockCountResult>> ShipToAmazonAsync(string marketplaceId, IReadOnlyList<HomeStockShipment> shipments, string? reference, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(shipments);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var now = clock.GetUtcNow();
        var tidyReference = Tidy(reference);
        string? error =
            shipments.Count == 0 ? "Enter how many units to send."
            : shipments.Count > InventoryItemService.MaxHomeStockRows ? $"Send at most {InventoryItemService.MaxHomeStockRows:N0} SKUs at a time."
            : shipments.FirstOrDefault(s => s.Units <= 0) is { } none ? $"Enter how many units of {none.Sku} to send, from 1 up."
            : shipments.FirstOrDefault(s => s.Units > s.Current) is { } over ? $"Can't send more {over.Sku} than the {over.Current:N0} at home."
            : tidyReference?.Length > MaxReferenceLength ? $"The shipment id can be at most {MaxReferenceLength} characters."
            : null;
        if (error is not null)
        {
            return Result.Failure<HomeStockCountResult>(error);
        }

        // A send is the count going down by the units shipped, logged as Shipped to Amazon. Going through the
        // count-apply path keeps it in one serializable transaction and skips SKUs whose balance moved since
        // the amounts were entered, instead of subtracting from a number the user never saw.
        var changes = shipments
            .GroupBy(s => s.Sku, StringComparer.Ordinal)
            .Select(g => g.Last())
            .Select(s => new HomeStockCountChange(s.Sku, s.Current, s.Current - s.Units))
            .ToList();
        var template = new HomeStockLedgerWrite(marketplaceId, string.Empty, HomeStockMovementType.ShippedToAmazon, 0, now, tidyReference, null, null, now, user);
        var baselines = await BaselinesAsync(marketplaceId, cancellationToken).ConfigureAwait(false);
        var (applied, stale) = await repository.ApplyCountsAsync(
            marketplaceId, changes, HomeStockMovementType.ShippedToAmazon, HomeStockMovementType.ShippedToAmazon, template, baselines, cancellationToken).ConfigureAwait(false);

        var unitsOut = -applied.Sum(c => c.Difference);
        LogShipped(applied.Count, unitsOut, stale.Count, marketplaceId);
        if (applied.Count > 0)
        {
            alerts.Request();
            await refresh.RequestAsync(marketplaceId, user, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new HomeStockCountResult(applied.Count, 0, unitsOut, stale));
    }

    /// <inheritdoc/>
    public async Task<Result> ReverseAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var (outcome, reversalId) = await repository.ReverseAsync(marketplaceId, id, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case HomeStockLedgerOutcome.Recorded:
                LogReversed(id, reversalId, marketplaceId);
                alerts.Request();
                return Result.Success();
            case HomeStockLedgerOutcome.NotFound:
                return Result.Failure("That entry no longer exists.");
            case HomeStockLedgerOutcome.CannotReverse:
                return Result.Failure("That entry has already been reversed, or is itself a reversal or an opening balance.");
            case HomeStockLedgerOutcome.WouldGoNegative:
                return Result.Failure("Reversing it would take home stock below zero, because units it added have since gone out. Log a count correction instead.");
            case HomeStockLedgerOutcome.AlreadyAtAmazon:
                return Result.Failure("Amazon already shows some of this send, so those units did go out. Edit the send's units instead.");
            default:
                return Result.Failure("The entry could not be reversed.");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string>> CorrectAsync(string marketplaceId, HomeStockCorrectionInput input, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var baselines = input.Type == HomeStockMovementType.ShippedToAmazon ? await BaselinesAsync(marketplaceId, cancellationToken).ConfigureAwait(false) : NoBaselines;
        var (outcome, id, error) = await ApplyCorrectionAsync(marketplaceId, input, baselines, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
        return error is not null ? Result.Failure<string>(error)
            : outcome == HomeStockLedgerOutcome.Unchanged ? Result.Success("Nothing changed.")
            : Result.Success($"Entry #{input.Id} updated (logged as #{id}; the original is kept in the history).");
    }

    /// <inheritdoc/>
    public async Task<Result<HomeStockBulkCorrectionResult>> CorrectReasonAsync(
        string marketplaceId, IReadOnlyCollection<long> ids, HomeStockMovementType? type, string? note, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var unique = ids.Distinct().ToList();
        var newNote = Tidy(note);
        string? error =
            unique.Count == 0 ? "Tick the entries to change."
            : unique.Count > MaxBulkEntries ? $"Change at most {MaxBulkEntries:N0} entries at a time."
            : type is null && newNote is null ? "Choose a movement or enter a note to set."
            : null;
        if (error is not null)
        {
            return Result.Failure<HomeStockBulkCorrectionResult>(error);
        }

        var entries = (await repository.GetEntriesAsync(marketplaceId, unique, cancellationToken).ConfigureAwait(false)).ToDictionary(e => e.Id);
        var baselines = type == HomeStockMovementType.ShippedToAmazon ? await BaselinesAsync(marketplaceId, cancellationToken).ConfigureAwait(false) : NoBaselines;
        var updated = 0;
        var unchanged = 0;
        var skipped = new List<string>();

        // One logged time for the whole change, so it reads as one action in the ledger.
        var now = clock.GetUtcNow();

        // Each entry is its own correction, so one that can't change never holds the others back.
        foreach (var id in unique)
        {
            if (!entries.TryGetValue(id, out var entry))
            {
                skipped.Add($"#{id}: that entry no longer exists.");
                continue;
            }

            // A count correction or other movement keeps its own direction; received and shipped take theirs.
            var input = new HomeStockCorrectionInput(id, type ?? entry.Type, entry.Units, newNote ?? entry.Note);
            var (outcome, _, failure) = await ApplyCorrectionAsync(marketplaceId, input, baselines, now, user, cancellationToken).ConfigureAwait(false);
            if (failure is not null)
            {
                skipped.Add($"#{id} {entry.Sku}: {failure}");
            }
            else if (outcome == HomeStockLedgerOutcome.Unchanged)
            {
                unchanged++;
            }
            else
            {
                updated++;
            }
        }

        return Result.Success(new HomeStockBulkCorrectionResult(updated, unchanged, skipped));
    }

    /// <summary>Validates an edit and records it as a correction; the error is set when it can't be made.</summary>
    private async Task<(HomeStockLedgerOutcome Outcome, long? Id, string? Error)> ApplyCorrectionAsync(
        string marketplaceId, HomeStockCorrectionInput input, IReadOnlyDictionary<string, AmazonBaseline> baselines, DateTimeOffset now, string user, CancellationToken cancellationToken)
    {
        var note = Tidy(input.Note);
        var limit = InventoryItemService.MaxHomeStock;

        // Range-checked as a long first: Math.Abs(int.MinValue) would overflow.
        if (input.Units == 0 || Math.Abs((long)input.Units) > limit)
        {
            return (HomeStockLedgerOutcome.Unchanged, null, $"Enter the units, from 1 to {limit:N0} (+ adds, − removes for a count correction or other).");
        }

        // Received and shipped have a direction of their own, so only the amount typed matters.
        var units = input.Type switch
        {
            HomeStockMovementType.ReceivedFromSupplier => Math.Abs(input.Units),
            HomeStockMovementType.ShippedToAmazon => -Math.Abs(input.Units),
            _ => input.Units,
        };
        string? error =
            !Enum.IsDefined(input.Type) || input.Type == HomeStockMovementType.OpeningBalance ? "Choose what kind of movement this is."
            : input.Type == HomeStockMovementType.Other && note is null ? "Add a note saying what this movement was."
            : note?.Length > MaxNoteLength ? $"The note can be at most {MaxNoteLength} characters."
            : null;
        if (error is not null)
        {
            return (HomeStockLedgerOutcome.Unchanged, null, error);
        }

        var (outcome, correctionId) = await repository.CorrectAsync(
            marketplaceId, input.Id, input.Type, units, note, baselines, now, user, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case HomeStockLedgerOutcome.Recorded:
                LogCorrected(input.Id, correctionId, input.Type, units, marketplaceId);
                alerts.Request();
                return (outcome, correctionId, null);
            case HomeStockLedgerOutcome.Unchanged:
                return (outcome, null, null);
            case HomeStockLedgerOutcome.NotFound:
                return (outcome, null, "That entry no longer exists.");
            case HomeStockLedgerOutcome.CannotReverse:
                return (outcome, null, "That entry can't be edited: it was reversed or edited already, or is a reversal or an opening balance.");
            case HomeStockLedgerOutcome.WouldGoNegative:
                return (outcome, null, "That would take home stock below zero, because units have since gone out. Check the units, or log a count correction.");
            case HomeStockLedgerOutcome.AlreadyAtAmazon:
                return (outcome, null, "Amazon already shows some of this send, so it can't go below those units or stop being a send to Amazon.");
            default:
                return (outcome, null, "The entry could not be updated.");
        }
    }

    /// <inheritdoc/>
    public Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(request);

        return repository.ListAsync(marketplaceId, filter, request, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<PagedResult<HomeStockLedgerAction>> GetActionsAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(request);

        return repository.ListActionsAsync(marketplaceId, filter, request, MaxGroupRows, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Result<string>> UndoActionAsync(string marketplaceId, long entryId, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var result = await repository.ReverseActionAsync(marketplaceId, entryId, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
        return Undone(result, "action", marketplaceId);
    }

    /// <inheritdoc/>
    public async Task<Result<string>> ReverseManyAsync(string marketplaceId, IReadOnlyCollection<long> ids, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var unique = ids.Distinct().ToList();
        if (unique.Count == 0)
        {
            return Result.Failure<string>("Choose the entries to reverse.");
        }

        if (unique.Count > MaxBulkEntries)
        {
            return Result.Failure<string>($"Reverse at most {MaxBulkEntries:N0} entries at a time.");
        }

        var result = await repository.ReverseEntriesAsync(marketplaceId, unique, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
        return Undone(result, "selection", marketplaceId);
    }

    private Result<string> Undone((HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus) result, string what, string marketplaceId)
    {
        var (outcome, reversed, blocking) = result;
        var skus = string.Join(", ", blocking.Take(5)) + (blocking.Count > 5 ? " …" : string.Empty);
        switch (outcome)
        {
            case HomeStockLedgerOutcome.Recorded:
                LogUndone(what, reversed, marketplaceId);
                alerts.Request();
                return Result.Success($"Undone: {reversed:N0} entr{(reversed == 1 ? "y" : "ies")} reversed. The originals stay in the history.");
            case HomeStockLedgerOutcome.NotFound:
                return Result.Failure<string>($"That {what} no longer exists.");
            case HomeStockLedgerOutcome.CannotReverse:
                return Result.Failure<string>($"Nothing in that {what} can be undone: it was undone already, or is itself an undo or an opening balance.");
            case HomeStockLedgerOutcome.WouldGoNegative:
                return Result.Failure<string>($"Nothing was undone: it would take home stock below zero for {skus}, because units it added have since gone out. Log a count correction instead.");
            case HomeStockLedgerOutcome.AlreadyAtAmazon:
                return Result.Failure<string>($"Nothing was undone: Amazon already shows units of {skus} from this send, so they did go out.");
            default:
                return Result.Failure<string>($"The {what} could not be undone.");
        }
    }

    private static string? Validate(HomeStockMovementInput input, string sku, string? reference, string? note, DateTimeOffset now)
    {
        if (sku.Length == 0)
        {
            return "Choose a SKU.";
        }

        if (!Enum.IsDefined(input.Type) || input.Type == HomeStockMovementType.OpeningBalance)
        {
            return "Choose what kind of movement this is.";
        }

        var limit = InventoryItemService.MaxHomeStock;
        var quantityError = input.Type switch
        {
            HomeStockMovementType.CountCorrection when input.Quantity is < 0 || input.Quantity > limit
                => $"The counted total must be between 0 and {limit:N0}.",
            HomeStockMovementType.Other when input.Quantity == 0 || Math.Abs(input.Quantity) > limit
                => $"Enter the units added (positive) or removed (negative), up to {limit:N0}.",
            HomeStockMovementType.ReceivedFromSupplier or HomeStockMovementType.ShippedToAmazon when input.Quantity is <= 0 || input.Quantity > limit
                => $"Enter how many units, from 1 to {limit:N0}.",
            _ => null,
        };
        if (quantityError is not null)
        {
            return quantityError;
        }

        if (input.Type == HomeStockMovementType.Other && note is null)
        {
            return "Add a note saying what this movement was.";
        }

        if (reference?.Length > MaxReferenceLength)
        {
            return $"The reference can be at most {MaxReferenceLength} characters.";
        }

        if (note?.Length > MaxNoteLength)
        {
            return $"The note can be at most {MaxNoteLength} characters.";
        }

        return input.OccurredAt > now + FutureTolerance ? "The date can't be in the future." : null;
    }

    private static string Describe(HomeStockMovementInput input, string sku) => input.Type switch
    {
        HomeStockMovementType.ReceivedFromSupplier => $"Logged {input.Quantity:N0} {sku} received from the supplier.",
        HomeStockMovementType.ShippedToAmazon => $"Logged {input.Quantity:N0} {sku} shipped to Amazon.",
        HomeStockMovementType.CountCorrection => $"Set {sku}'s home stock to {input.Quantity:N0}.",
        _ => $"Logged {input.Quantity:+#,0;-#,0} {sku}.",
    };

    /// <summary>Each SKU's FBA stock now, to store on sends (read outside the ledger's serializable transaction).</summary>
    private async Task<IReadOnlyDictionary<string, AmazonBaseline>> BaselinesAsync(string marketplaceId, CancellationToken cancellationToken) =>
        (await inventory.GetPositionsAsync(marketplaceId, cancellationToken).ConfigureAwait(false))
            .ToDictionary(p => p.Sku, AmazonBaseline.Of, StringComparer.Ordinal);

    private static string? Tidy(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [LoggerMessage(Level = LogLevel.Information, Message = "Recorded home-stock entry {Id}: {Type} {Sku} {Units} in {MarketplaceId}")]
    private partial void LogRecorded(long? id, HomeStockMovementType type, string sku, int units, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied a count sheet in {MarketplaceId}: {Applied} entries, +{UnitsIn}/-{UnitsOut} units, {Stale} stale")]
    private partial void LogCountsApplied(int applied, int unitsIn, int unitsOut, int stale, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Logged shipments to Amazon in {MarketplaceId}: {Applied} entries, -{UnitsOut} units, {Stale} stale")]
    private partial void LogShipped(int applied, int unitsOut, int stale, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reversed home-stock entry {Id} with {ReversalId} in {MarketplaceId}")]
    private partial void LogReversed(long id, long? reversalId, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Undid a home-stock {What}: {Reversed} entries reversed in {MarketplaceId}")]
    private partial void LogUndone(string what, int reversed, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Corrected home-stock entry {Id} with {CorrectionId}: {Type} {Units} in {MarketplaceId}")]
    private partial void LogCorrected(long id, long? correctionId, HomeStockMovementType type, int units, string marketplaceId);
}
