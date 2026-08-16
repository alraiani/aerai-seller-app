using AERai.Seller.Application.Abstractions;
using AERai.Seller.Application.Bookkeeping;
using AERai.Seller.Domain;
using AERai.Seller.Domain.Staging;
using Xunit;

namespace AERai.Seller.Application.Tests.Bookkeeping;

public class BookkeepingExportServiceTests
{
    private static SettlementReport Settlement(decimal totalAmount = 93.50m) => new()
    {
        SettlementId = "1000",
        MarketplaceId = "Amazon.com",
        FinancialEventGroupStart = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
        FinancialEventGroupEnd = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
        TotalAmount = totalAmount,
        Currency = "USD",
        DepositDate = new DateTimeOffset(2026, 7, 16, 0, 0, 0, TimeSpan.Zero),
    };

    private static SettlementLineItem LineItem(string settlementId, string amountType, string amountDescription, decimal amount) => new()
    {
        SettlementId = settlementId,
        AmountType = amountType,
        AmountDescription = amountDescription,
        Amount = amount,
        Currency = "USD",
        PostedDate = default,
    };

    private sealed class FakeSettlementRepository(SettlementReport? settlement, IReadOnlyList<SettlementLineItem> lineItems) : ISettlementRepository
    {
        public Task UpsertAsync(IEnumerable<SettlementReport> s, IEnumerable<SettlementLineItem> l, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<SettlementReport>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SettlementReport>>(settlement is null ? [] : [settlement]);
        public Task<IReadOnlyList<SettlementLineItem>> GetLineItemsAsync(string settlementId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SettlementLineItem>>(lineItems.Where(l => l.SettlementId == settlementId).ToList());
        public Task<IReadOnlyList<(string AmountType, string AmountDescription)>> GetDistinctCategoriesAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
    }

    private sealed class FakeMappingRepository(IReadOnlyList<BookkeepingAccountMapping> mappings) : IBookkeepingAccountMappingRepository
    {
        public Task<IReadOnlyList<BookkeepingAccountMapping>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(mappings);
        public Task UpsertAsync(BookkeepingAccountMapping mapping, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeSettingsRepository(string? depositAccountName) : IBookkeepingSettingsRepository
    {
        public Task<BookkeepingSettings> GetAsync(CancellationToken ct = default)
            => Task.FromResult(new BookkeepingSettings { DepositAccountName = depositAccountName });
        public Task SaveAsync(BookkeepingSettings settings, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeExportRepository : IBookkeepingExportRepository
    {
        public List<BookkeepingExportRecord> Inserted { get; } = [];
        public Task InsertAsync(BookkeepingExportRecord record, CancellationToken ct = default)
        {
            Inserted.Add(record);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<BookkeepingExportRecord>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<BookkeepingExportRecord>>(Inserted);
    }

    private sealed class FakeExportFileStore : IExportFileStore
    {
        public List<(string FileName, string Content)> SavedFiles { get; } = [];
        public Task<string> SaveAsync(string fileName, string content, CancellationToken ct = default)
        {
            SavedFiles.Add((fileName, content));
            return Task.FromResult($@"C:\fake\{fileName}");
        }
    }

    private static BookkeepingExportService CreateService(
        SettlementReport? settlement,
        IReadOnlyList<SettlementLineItem> lineItems,
        IReadOnlyList<BookkeepingAccountMapping> mappings,
        FakeExportRepository exportRepository,
        FakeExportFileStore fileStore,
        string? depositAccountName = "Amazon Clearing")
        => new(
            new FakeSettlementRepository(settlement, lineItems),
            new FakeMappingRepository(mappings),
            new FakeSettingsRepository(depositAccountName),
            exportRepository,
            fileStore,
            TimeProvider.System);

    [Fact]
    public async Task PreviewAsync_FlagsUnmappedCategories()
    {
        var settlement = Settlement();
        var lineItems = new List<SettlementLineItem> { LineItem(settlement.SettlementId, "ItemPrice", "Principal", 100m) };
        var service = CreateService(settlement, lineItems, mappings: [], new FakeExportRepository(), new FakeExportFileStore());

        var summary = await service.PreviewAsync(settlement.SettlementId);

        Assert.True(summary.HasUnmappedCategories);
        Assert.Single(summary.UnmappedCategories);
    }

    [Fact]
    public async Task PreviewAsync_Throws_WhenSettlementNotFound()
    {
        var service = CreateService(settlement: null, [], [], new FakeExportRepository(), new FakeExportFileStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PreviewAsync("does-not-exist"));
    }

    [Fact]
    public async Task ExportAsync_Throws_WhenCategoriesUnmapped_AndWritesNothing()
    {
        var settlement = Settlement();
        var lineItems = new List<SettlementLineItem> { LineItem(settlement.SettlementId, "ItemPrice", "Principal", 100m) };
        var exportRepository = new FakeExportRepository();
        var fileStore = new FakeExportFileStore();
        var service = CreateService(settlement, lineItems, mappings: [], exportRepository, fileStore);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(settlement.SettlementId));

        Assert.Empty(fileStore.SavedFiles);
        Assert.Empty(exportRepository.Inserted);
    }

    [Fact]
    public async Task ExportAsync_WritesFileAndRecordsExport_WhenFullyMapped()
    {
        var settlement = Settlement(93.50m);
        var lineItems = new List<SettlementLineItem> { LineItem(settlement.SettlementId, "ItemPrice", "Principal", 93.50m) };
        var mappings = new List<BookkeepingAccountMapping>
        {
            new() { AmountType = "ItemPrice", AmountDescription = "Principal", QuickBooksAccountName = "Sales Income", UpdatedAt = default },
        };
        var exportRepository = new FakeExportRepository();
        var fileStore = new FakeExportFileStore();
        var service = CreateService(settlement, lineItems, mappings, exportRepository, fileStore, depositAccountName: "Amazon Clearing");

        var record = await service.ExportAsync(settlement.SettlementId);

        Assert.Single(fileStore.SavedFiles);
        var inserted = Assert.Single(exportRepository.Inserted);
        Assert.Same(inserted, record);
        Assert.Equal(settlement.SettlementId, record.SettlementId);
        Assert.Equal(93.50m, record.NetTotal);
        Assert.Equal(settlement.FinancialEventGroupStart, record.PeriodStart);
        Assert.Equal(settlement.FinancialEventGroupEnd, record.PeriodEnd);
    }

    [Fact]
    public async Task ExportAsync_Throws_WhenDepositAccountNotConfigured()
    {
        var settlement = Settlement();
        var lineItems = new List<SettlementLineItem> { LineItem(settlement.SettlementId, "ItemPrice", "Principal", 93.50m) };
        var mappings = new List<BookkeepingAccountMapping>
        {
            new() { AmountType = "ItemPrice", AmountDescription = "Principal", QuickBooksAccountName = "Sales Income", UpdatedAt = default },
        };
        var exportRepository = new FakeExportRepository();
        var fileStore = new FakeExportFileStore();
        var service = CreateService(settlement, lineItems, mappings, exportRepository, fileStore, depositAccountName: null);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportAsync(settlement.SettlementId));

        Assert.Empty(fileStore.SavedFiles);
        Assert.Empty(exportRepository.Inserted);
    }
}
