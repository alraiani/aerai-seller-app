using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Application.Inventory;

/// <summary>Default <see cref="IHomeStockLedgerService"/>.</summary>
/// <param name="repository">Ledger persistence.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class HomeStockLedgerService(IHomeStockLedgerRepository repository, TimeProvider clock, ILogger<HomeStockLedgerService> logger) : IHomeStockLedgerService
{
    /// <summary>Longest reference accepted.</summary>
    public const int MaxReferenceLength = 100;

    /// <summary>Longest note accepted.</summary>
    public const int MaxNoteLength = 500;

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

        var write = new HomeStockLedgerWrite(marketplaceId, sku, input.Type, units, input.OccurredAt ?? now, reference, note, null, now, user);
        var (outcome, id) = await repository.RecordAsync(write, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case HomeStockLedgerOutcome.Recorded:
                LogRecorded(id, input.Type, sku, units, marketplaceId);
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
    public async Task<Result> ReverseAsync(string marketplaceId, long id, string user, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(user);

        var (outcome, reversalId) = await repository.ReverseAsync(marketplaceId, id, clock.GetUtcNow(), user, cancellationToken).ConfigureAwait(false);
        switch (outcome)
        {
            case HomeStockLedgerOutcome.Recorded:
                LogReversed(id, reversalId, marketplaceId);
                return Result.Success();
            case HomeStockLedgerOutcome.NotFound:
                return Result.Failure("That entry no longer exists.");
            case HomeStockLedgerOutcome.CannotReverse:
                return Result.Failure("That entry has already been reversed, or is itself a reversal or an opening balance.");
            case HomeStockLedgerOutcome.WouldGoNegative:
                return Result.Failure("Reversing it would take home stock below zero, because units it added have since gone out. Log a count correction instead.");
            default:
                return Result.Failure("The entry could not be reversed.");
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

    private static string? Tidy(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    [LoggerMessage(Level = LogLevel.Information, Message = "Recorded home-stock entry {Id}: {Type} {Sku} {Units} in {MarketplaceId}")]
    private partial void LogRecorded(long? id, HomeStockMovementType type, string sku, int units, string marketplaceId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Reversed home-stock entry {Id} with {ReversalId} in {MarketplaceId}")]
    private partial void LogReversed(long id, long? reversalId, string marketplaceId);
}
