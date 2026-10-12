using System.Text;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Ingestion;

public sealed class BlobRefreshServiceTests
{
    private const string OrdersTsv =
        "amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\n" +
        "111-1\t2026-10-01T10:00:00Z\tShipped\tA-1\t2\t19.98\n";

    private const string SettlementTsv =
        "settlement-id\ttransaction-type\tamount-type\tamount\n" +
        "S1\tOrder\tItemPrice\t10.00\n";

    private const string HeaderOnlyTsv = "amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\n";

    private const string OrdersPage = """
        {"orders":[{"orderId":"111-9","createdTime":"2026-10-01T10:00:00Z","lastUpdatedTime":"2026-10-01T12:00:00Z",
          "fulfillment":{"fulfillmentStatus":"SHIPPED"},
          "orderItems":[{"orderItemId":"1","quantityOrdered":2,"product":{"sellerSku":"A-1","price":{"unitPrice":{"amount":"9.99","currencyCode":"USD"}}}}]}]}
        """;

    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeRawFileStore _rawFiles = new();
    private readonly FakeStagingRepository _staged = new();
    private readonly FakePromotionService _promotion = new();
    private readonly FakeAmazonOrderRepository _orders = new();
    private readonly FakeMarketplaceQueries _marketplaces = new();
    private readonly FakeTimeProvider _clock = new(Now);
    private readonly FakeAppliedBlobRepository _applied;

    public BlobRefreshServiceTests() => _applied = new FakeAppliedBlobRepository(_staged);

    private BlobRefreshService CreateService()
    {
        var staging = new StagingImportService(
            [new OrderLineMapper(), new SettlementLineMapper()],
            _rawFiles, _staged, new FakeImportBatchQueries(), Options.Create(new ImportOptions()), _clock,
            NullLogger<StagingImportService>.Instance);
        return new BlobRefreshService(_rawFiles, staging, _promotion, _orders, _applied, _marketplaces, _clock, NullLogger<BlobRefreshService>.Instance);
    }

    private static BlobRefreshRequest Request(bool promote = true, bool reprocess = false, string marketplaceId = MarketplaceIds.UnitedStates) =>
        new(marketplaceId, promote, reprocess);

    private async Task UploadAsync(string path, string content, string? marketplaceId = null, bool writtenByApp = false, DateTimeOffset? createdAt = null)
    {
        var metadata = new Dictionary<string, string>();
        if (marketplaceId is not null)
        {
            metadata["marketplaceid"] = marketplaceId;
        }

        if (writtenByApp)
        {
            metadata["syncrunid"] = "7";
        }

        await _rawFiles.SaveAsync(path, new MemoryStream(Encoding.UTF8.GetBytes(content)), metadata, CancellationToken.None);
        if (createdAt is not null)
        {
            _rawFiles.CreatedAt[path] = createdAt.Value;
        }
    }

    [Fact]
    public async Task RefreshAsync_UploadedReport_IsStagedForTheDefaultMarketplaceAndPromoted()
    {
        await UploadAsync("orders/2026/10/01/abc-orders.tsv", OrdersTsv);

        var result = await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal((1, 1), (result.Value.Loaded, result.Value.Promoted));
        var batch = Assert.Single(_staged.Saved);
        Assert.Equal((ImportSource.Orders, MarketplaceIds.UnitedStates, "orders/2026/10/01/abc-orders.tsv", "abc-orders.tsv"), (batch.Source, batch.MarketplaceId, batch.RawFilePath, batch.FileName));
        Assert.Equal([batch.Id], _promotion.Promoted);
    }

    [Fact]
    public async Task RefreshAsync_FileNamingAMarketplace_UsesThatMarketplaceNotTheDefault()
    {
        await UploadAsync("settlements/2026/10/01/abc-s.tsv", SettlementTsv, marketplaceId: MarketplaceIds.Canada);

        await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        var batch = Assert.Single(_staged.Saved);
        Assert.Equal((ImportSource.Settlements, MarketplaceIds.Canada), (batch.Source, batch.MarketplaceId));
    }

    [Fact]
    public async Task RefreshAsync_RunTwice_SecondRunLoadsNothingNew()
    {
        await UploadAsync("orders/2026/10/01/a.tsv", OrdersTsv);
        await UploadAsync("ordersapi/2026/10/01/p1.json", OrdersPage);
        await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        var again = await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        Assert.Equal((0, 2), (again.Value.Loaded, again.Value.AlreadyLoaded));
        Assert.Single(_staged.Saved);
        Assert.Single(_orders.Writes);
    }

    [Fact]
    public async Task RefreshAsync_Reprocess_LoadsFilesAlreadyLoadedAgain()
    {
        await UploadAsync("ordersapi/2026/10/01/p1.json", OrdersPage);
        await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        var again = await CreateService().RefreshAsync(Request(reprocess: true), "ops", CancellationToken.None);

        Assert.Equal(1, again.Value.Loaded);
        Assert.Equal(2, _orders.Writes.Count);
    }

    [Fact]
    public async Task RefreshAsync_OrdersApiPage_WritesItsOrdersAndRecordsTheFile()
    {
        await UploadAsync("ordersapi/2026/10/01/p1.json", OrdersPage);

        var result = await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        Assert.Equal((1, 1), (result.Value.Loaded, result.Value.OrdersWritten));
        var write = Assert.Single(_orders.Writes);
        Assert.Equal(MarketplaceIds.UnitedStates, write.MarketplaceId);
        var order = Assert.Single(write.Orders);
        Assert.Equal(("111-9", "Shipped", 19.98m), (order.AmazonOrderId, order.Status, order.Lines.Single().ItemPrice));
        Assert.Equal("1 order", Assert.Single(_applied.Ledger).Outcome);
    }

    [Fact]
    public async Task RefreshAsync_PageTheAppSavedItself_IsSkippedBecauseItWasAppliedWhenItArrived()
    {
        await UploadAsync("ordersapi/2026/10/01/p1.json", OrdersPage, writtenByApp: true);

        var result = await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        Assert.Equal((0, 1), (result.Value.Loaded, result.Value.AlreadyLoaded));
        Assert.Empty(_orders.Writes);
    }

    [Fact]
    public async Task RefreshAsync_SnapshotFolders_AreNeverLoaded()
    {
        await UploadAsync("fbainventory/2026/10/01/x.tsv", "sku\tquantity\nA-1\t5\n");
        await UploadAsync("awdinventory/2026/10/01/x.json", "{}");
        await UploadAsync("somethingelse/2026/10/01/x.txt", "hello");

        var result = await CreateService().RefreshAsync(Request(reprocess: true), "ops", CancellationToken.None);

        Assert.Equal((0, 2), (result.Value.Loaded, result.Value.SnapshotFiles));
        Assert.Empty(_staged.Saved);
        Assert.Empty(_orders.Writes);
    }

    [Fact]
    public async Task RefreshAsync_HeaderOnlyFile_IsRecordedAsEmptyAndNotTriedAgain()
    {
        await UploadAsync("orders/2026/10/01/empty.tsv", HeaderOnlyTsv);

        var first = await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);
        var second = await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        Assert.Equal((0, 1), (first.Value.Loaded, first.Value.Empty));
        Assert.Empty(first.Value.Problems);
        Assert.Equal((0, 0, 1), (second.Value.Loaded, second.Value.Empty, second.Value.AlreadyLoaded));
    }

    [Fact]
    public async Task RefreshAsync_OneBadFile_IsReportedAndTheRestStillLoad()
    {
        await UploadAsync("ordersapi/2026/10/01/bad.json", "this is not json", createdAt: Now.AddDays(-2));
        await UploadAsync("orders/2026/10/01/good.tsv", OrdersTsv, createdAt: Now.AddDays(-1));

        var result = await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        Assert.Equal(1, result.Value.Loaded);
        var problem = Assert.Single(result.Value.Problems);
        Assert.StartsWith("ordersapi/2026/10/01/bad.json:", problem, StringComparison.Ordinal);
        Assert.Single(_staged.Saved);
    }

    [Fact]
    public async Task RefreshAsync_NoPromotion_StagesOnly()
    {
        await UploadAsync("orders/2026/10/01/a.tsv", OrdersTsv);

        var result = await CreateService().RefreshAsync(Request(promote: false), "ops", CancellationToken.None);

        Assert.Equal((1, 0), (result.Value.Loaded, result.Value.Promoted));
        Assert.Empty(_promotion.Promoted);
    }

    [Fact]
    public async Task RefreshAsync_LoadsOldestFirst()
    {
        await UploadAsync("ordersapi/2026/10/02/new.json", OrdersPage.Replace("111-9", "NEWER", StringComparison.Ordinal), createdAt: Now);
        await UploadAsync("ordersapi/2026/10/01/old.json", OrdersPage.Replace("111-9", "OLDER", StringComparison.Ordinal), createdAt: Now.AddDays(-5));

        await CreateService().RefreshAsync(Request(), "ops", CancellationToken.None);

        Assert.Equal(["OLDER", "NEWER"], _orders.Writes.Select(w => w.Orders.Single().AmazonOrderId));
    }

    [Theory]
    [InlineData(MarketplaceIds.UnitedKingdom)] // Seeded inactive.
    [InlineData("NOT-A-MARKETPLACE")]
    public async Task RefreshAsync_DefaultMarketplaceInactiveOrUnknown_Fails(string marketplaceId)
    {
        await UploadAsync("orders/2026/10/01/a.tsv", OrdersTsv);

        var result = await CreateService().RefreshAsync(Request(marketplaceId: marketplaceId), "ops", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_staged.Saved);
    }

    [Fact]
    public async Task PreviewAsync_CountsWhatARefreshWouldDo_WithoutDoingIt()
    {
        await UploadAsync("orders/2026/10/01/a.tsv", OrdersTsv);
        await UploadAsync("settlements/2026/10/01/s.tsv", SettlementTsv);
        await UploadAsync("ordersapi/2026/10/01/p1.json", OrdersPage);
        await UploadAsync("ordersapi/2026/10/01/p2.json", OrdersPage, writtenByApp: true);
        await UploadAsync("fbainventory/2026/10/01/x.tsv", "x");

        var preview = await CreateService().PreviewAsync(Request(), CancellationToken.None);

        Assert.Equal((2, 1, 1, 1), (preview.NewReportFiles, preview.NewOrderPages, preview.AlreadyLoaded, preview.SnapshotFiles));
        Assert.Equal(3, preview.NewFiles);
        Assert.Empty(_staged.Saved);
        Assert.Empty(_orders.Writes);
    }
}
