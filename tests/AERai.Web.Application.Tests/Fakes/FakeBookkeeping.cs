using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Bookkeeping;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory bookkeeping persistence: one object is both the read and the write side.</summary>
internal sealed class FakeBookkeeping : IBookkeepingQueries, IBookkeepingStore
{
    public Dictionary<string, SettlementBooks> Settlements { get; } = new(StringComparer.Ordinal);

    public List<BookkeepingAccountMapping> Mappings { get; } = [];

    public Dictionary<string, string> DepositAccounts { get; } = new(StringComparer.Ordinal);

    public List<BookkeepingExport> Exports { get; } = [];

    public Task<SettlementBooks?> GetSettlementAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken) =>
        Task.FromResult(Settlements.TryGetValue(settlementId, out var s) && s.MarketplaceId == marketplaceId ? s : null);

    public Task<IReadOnlyList<SettlementCategoryTotal>> GetCategoriesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<SettlementCategoryTotal>>([.. Settlements.Values
            .SelectMany(s => s.Categories)
            .GroupBy(c => (c.AmountType, c.AmountDescription))
            .Select(g => new SettlementCategoryTotal(g.Key.AmountType, g.Key.AmountDescription, g.Sum(c => c.Amount), g.Sum(c => c.LineCount)))]);

    public Task<IReadOnlyList<BookkeepingAccountMapping>> GetMappingsAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BookkeepingAccountMapping>>([.. Mappings]);

    public Task<string?> GetDepositAccountAsync(string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult(DepositAccounts.GetValueOrDefault(marketplaceId));

    public Task<IReadOnlyList<BookkeepingExportSummary>> GetExportsAsync(string marketplaceId, int maxCount, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<BookkeepingExportSummary>>([.. Exports
            .Where(e => e.MarketplaceId == marketplaceId)
            .OrderByDescending(e => e.GeneratedAt)
            .Take(maxCount)
            .Select(e => new BookkeepingExportSummary(e.Id, e.SettlementId, e.FileName, e.NetTotal, e.Currency, e.GeneratedAt, e.GeneratedBy))]);

    public Task<BookkeepingExportFile?> GetExportFileAsync(string marketplaceId, long exportId, CancellationToken cancellationToken) =>
        Task.FromResult(Exports.Where(e => e.Id == exportId && e.MarketplaceId == marketplaceId).Select(e => new BookkeepingExportFile(e.FileName, e.Content)).FirstOrDefault());

    public Task SaveMappingsAsync(IReadOnlyCollection<AccountMappingInput> mappings, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken)
    {
        foreach (var input in mappings)
        {
            Mappings.RemoveAll(m => m.AmountType == input.AmountType && m.AmountDescription == input.AmountDescription);
            if (!string.IsNullOrWhiteSpace(input.QuickBooksAccountName))
            {
                Mappings.Add(new BookkeepingAccountMapping
                {
                    AmountType = input.AmountType, AmountDescription = input.AmountDescription,
                    QuickBooksAccountName = input.QuickBooksAccountName, UpdatedAt = updatedAt, UpdatedBy = updatedBy,
                });
            }
        }

        return Task.CompletedTask;
    }

    public Task SaveDepositAccountAsync(string marketplaceId, string? depositAccountName, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(depositAccountName))
        {
            DepositAccounts.Remove(marketplaceId);
        }
        else
        {
            DepositAccounts[marketplaceId] = depositAccountName;
        }

        return Task.CompletedTask;
    }

    public Task<long> AddExportAsync(BookkeepingExport export, CancellationToken cancellationToken)
    {
        export.Id = Exports.Count + 1;
        Exports.Add(export);
        return Task.FromResult(export.Id);
    }
}
