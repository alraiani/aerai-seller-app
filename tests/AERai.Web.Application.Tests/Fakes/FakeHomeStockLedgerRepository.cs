using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IHomeStockLedgerRepository"/> with the same balance rules as the SQL one.</summary>
internal sealed class FakeHomeStockLedgerRepository : IHomeStockLedgerRepository
{
    public HashSet<string> Skus { get; } = new(StringComparer.Ordinal);

    public List<HomeStockLedgerWrite> Entries { get; } = [];

    public int Balance(string marketplaceId, string sku) =>
        Entries.Where(e => e.MarketplaceId == marketplaceId && e.Sku == sku).Sum(e => e.Units);

    public Task<(HomeStockLedgerOutcome Outcome, long? Id)> RecordAsync(HomeStockLedgerWrite write, CancellationToken cancellationToken)
    {
        if (!Skus.Contains(write.Sku))
        {
            return Task.FromResult<(HomeStockLedgerOutcome, long?)>((HomeStockLedgerOutcome.NotFound, null));
        }

        var current = Balance(write.MarketplaceId, write.Sku);
        var change = write.Type == HomeStockMovementType.CountCorrection ? write.Units - current : write.Units;
        if (change == 0)
        {
            return Task.FromResult<(HomeStockLedgerOutcome, long?)>((HomeStockLedgerOutcome.Unchanged, null));
        }

        if (current + change < 0)
        {
            return Task.FromResult<(HomeStockLedgerOutcome, long?)>((HomeStockLedgerOutcome.WouldGoNegative, null));
        }

        Entries.Add(write with { Units = change });
        return Task.FromResult<(HomeStockLedgerOutcome, long?)>((HomeStockLedgerOutcome.Recorded, Entries.Count));
    }

    public Task<(HomeStockLedgerOutcome Outcome, long? Id)> ReverseAsync(string marketplaceId, long id, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken)
    {
        if (id < 1 || id > Entries.Count)
        {
            return Task.FromResult<(HomeStockLedgerOutcome, long?)>((HomeStockLedgerOutcome.NotFound, null));
        }

        var original = Entries[(int)id - 1];
        if (original.ReversesId is not null || Entries.Any(e => e.ReversesId == id))
        {
            return Task.FromResult<(HomeStockLedgerOutcome, long?)>((HomeStockLedgerOutcome.CannotReverse, null));
        }

        if (Balance(marketplaceId, original.Sku) - original.Units < 0)
        {
            return Task.FromResult<(HomeStockLedgerOutcome, long?)>((HomeStockLedgerOutcome.WouldGoNegative, null));
        }

        Entries.Add(original with { Units = -original.Units, ReversesId = id, CreatedAt = createdAt, CreatedBy = createdBy });
        return Task.FromResult<(HomeStockLedgerOutcome, long?)>((HomeStockLedgerOutcome.Recorded, Entries.Count));
    }

    public Task<(IReadOnlyList<HomeStockCountChange> Applied, IReadOnlyList<string> Stale)> ApplyCountsAsync(
        string marketplaceId, IReadOnlyList<HomeStockCountChange> changes, HomeStockMovementType increaseType, HomeStockMovementType decreaseType, HomeStockLedgerWrite template, IReadOnlyDictionary<string, AmazonBaseline> amazonBaselines, CancellationToken cancellationToken)
    {
        var applied = new List<HomeStockCountChange>();
        var stale = new List<string>();
        foreach (var change in changes)
        {
            if (!Skus.Contains(change.Sku) || Balance(marketplaceId, change.Sku) != change.Current)
            {
                stale.Add(change.Sku);
                continue;
            }

            var type = change.Difference > 0 ? increaseType : decreaseType;
            var baseline = type == HomeStockMovementType.ShippedToAmazon ? amazonBaselines.GetValueOrDefault(change.Sku) ?? AmazonBaseline.Of(null) : null;
            Entries.Add(template with { Sku = change.Sku, Type = type, Units = change.Difference, AmazonBaseline = baseline });
            applied.Add(change);
        }

        return Task.FromResult<(IReadOnlyList<HomeStockCountChange>, IReadOnlyList<string>)>((applied, stale));
    }

    public Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PagedResult<HomeStockLedgerEntry>([], 0, 1, request.SafePageSize));

    public HomeStockLedgerOutcome CorrectOutcome { get; set; } = HomeStockLedgerOutcome.Recorded;

    public (long Id, HomeStockMovementType Type, int Units, string? Note, bool HadBaselines)? LastCorrection { get; private set; }

    public List<(long Id, HomeStockMovementType Type, int Units, string? Note)> Corrections { get; } = [];

    public List<HomeStockLedgerEntry> Listed { get; } = [];

    public Task<IReadOnlyList<HomeStockLedgerEntry>> GetEntriesAsync(string marketplaceId, IReadOnlyCollection<long> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HomeStockLedgerEntry>>(Listed.Where(e => ids.Contains(e.Id)).ToList());

    public Task<(HomeStockLedgerOutcome Outcome, long? Id)> CorrectAsync(
        string marketplaceId,
        long id,
        HomeStockMovementType type,
        int units,
        string? note,
        IReadOnlyDictionary<string, AmazonBaseline> amazonBaselines,
        DateTimeOffset createdAt,
        string createdBy,
        CancellationToken cancellationToken)
    {
        LastCorrection = (id, type, units, note, amazonBaselines.Count > 0);
        Corrections.Add((id, type, units, note));

        // Like the SQL repository, an edit that changes nothing records nothing.
        var listed = Listed.FirstOrDefault(e => e.Id == id);
        var outcome = listed is not null && listed.Type == type && listed.Units == units && listed.Note == note ? HomeStockLedgerOutcome.Unchanged : CorrectOutcome;
        return Task.FromResult<(HomeStockLedgerOutcome, long?)>((outcome, outcome == HomeStockLedgerOutcome.Recorded ? 99 : null));
    }

    public List<HomeStockLedgerAction> Actions { get; } = [];

    public (PageRequest Request, int MaxRows)? LastActionsCall { get; private set; }

    public Task<PagedResult<HomeStockLedgerAction>> ListActionsAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, int maxRows, CancellationToken cancellationToken)
    {
        LastActionsCall = (request, maxRows);
        return Task.FromResult(new PagedResult<HomeStockLedgerAction>(Actions, Actions.Count, request.SafePage, request.SafePageSize));
    }

    public Task<(HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus)> ReverseActionAsync(
        string marketplaceId, long entryId, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken)
    {
        if (entryId < 1 || entryId > Entries.Count)
        {
            return Task.FromResult<(HomeStockLedgerOutcome, int, IReadOnlyList<string>)>((HomeStockLedgerOutcome.NotFound, 0, []));
        }

        var anchor = Entries[(int)entryId - 1];
        var ids = Entries.Select((e, i) => (Entry: e, Id: (long)i + 1))
            .Where(x => x.Entry.CreatedAt == anchor.CreatedAt && x.Entry.CreatedBy == anchor.CreatedBy)
            .Select(x => x.Id)
            .ToList();
        return ReverseEntriesAsync(marketplaceId, ids, createdAt, createdBy, cancellationToken);
    }

    public Task<(HomeStockLedgerOutcome Outcome, int Reversed, IReadOnlyList<string> BlockingSkus)> ReverseEntriesAsync(
        string marketplaceId, IReadOnlyCollection<long> ids, DateTimeOffset createdAt, string createdBy, CancellationToken cancellationToken)
    {
        // The same all-or-nothing rules as the SQL repository: reversible entries only, checked per SKU before writing.
        var reversible = ids
            .Where(id => id >= 1 && id <= Entries.Count)
            .Select(id => (Id: id, Entry: Entries[(int)id - 1]))
            .Where(x => x.Entry.MarketplaceId == marketplaceId && x.Entry.ReversesId is null && x.Entry.Type != HomeStockMovementType.OpeningBalance && !Entries.Any(e => e.ReversesId == x.Id))
            .ToList();
        if (reversible.Count == 0)
        {
            return Task.FromResult<(HomeStockLedgerOutcome, int, IReadOnlyList<string>)>((HomeStockLedgerOutcome.CannotReverse, 0, []));
        }

        var negative = reversible.GroupBy(x => x.Entry.Sku).Where(g => Balance(marketplaceId, g.Key) - g.Sum(x => x.Entry.Units) < 0).Select(g => g.Key).ToList();
        if (negative.Count > 0)
        {
            return Task.FromResult<(HomeStockLedgerOutcome, int, IReadOnlyList<string>)>((HomeStockLedgerOutcome.WouldGoNegative, 0, negative));
        }

        foreach (var (id, original) in reversible)
        {
            Entries.Add(original with { Units = -original.Units, ReversesId = id, CreatedAt = createdAt, CreatedBy = createdBy });
        }

        return Task.FromResult<(HomeStockLedgerOutcome, int, IReadOnlyList<string>)>((HomeStockLedgerOutcome.Recorded, reversible.Count, []));
    }
}
