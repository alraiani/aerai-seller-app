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

    public Task<PagedResult<HomeStockLedgerEntry>> ListAsync(string marketplaceId, HomeStockLedgerFilter filter, PageRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new PagedResult<HomeStockLedgerEntry>([], 0, 1, request.SafePageSize));
}
