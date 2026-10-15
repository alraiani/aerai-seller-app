using System.Text;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Imports;

public sealed class StagingImportServiceTests
{
    private const string OrdersCsv =
        "amazon-order-id,purchase-date,order-status,sku,quantity,item-price\n" +
        "111-1,2026-10-01T10:00:00Z,Shipped,A-1,2,19.98\n" +
        "111-2,2026-10-01T11:00:00Z,Shipped,B-2,1,9.99\n";

    private readonly FakeStagingRepository _repository = new();
    private readonly FakeRawFileStore _rawFiles = new();
    private readonly FakeImportBatchQueries _batches = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    private StagingImportService CreateService(ImportOptions? options = null) => new(
        [new OrderLineMapper(), new InventoryRowMapper(), new SettlementLineMapper(), new FbaReservedInventoryRowMapper()],
        _rawFiles,
        _repository,
        _batches,
        Options.Create(options ?? new ImportOptions()),
        _clock,
        NullLogger<StagingImportService>.Instance);

    private static ImportFileCommand Command(
        string content, string fileName = "orders.csv", ImportSource source = ImportSource.Orders, string marketplaceId = MarketplaceIds.UnitedStates)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new ImportFileCommand(source, marketplaceId, fileName, bytes.Length, new MemoryStream(bytes), "ops@aeraigroup.com");
    }

    [Fact]
    public async Task ImportAsync_ValidOrdersFile_StagesEveryRowAsReceivedBatch()
    {
        var result = await CreateService().ImportAsync(Command(OrdersCsv), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.RowCount);

        var batch = Assert.Single(_repository.Saved);
        Assert.Equal(ImportBatchStatus.Received, batch.Status);
        Assert.Equal(_clock.GetUtcNow(), batch.UploadedAt);
        Assert.Equal("ops@aeraigroup.com", batch.UploadedBy);
        Assert.Equal(["111-1", "111-2"], batch.OrderLines.Select(l => l.AmazonOrderId));
        Assert.Equal("19.98", batch.OrderLines[0].ItemPrice);
    }

    [Fact]
    public async Task ImportAsync_ValidFile_LandsUntouchedRawFileAndLinksItToTheBatch()
    {
        await CreateService().ImportAsync(Command(OrdersCsv), CancellationToken.None);

        var (path, file) = Assert.Single(_rawFiles.Files);
        Assert.StartsWith("orders/2026/10/02/", path, StringComparison.Ordinal);
        Assert.EndsWith("-orders.csv", path, StringComparison.Ordinal);
        Assert.Equal(OrdersCsv, Encoding.UTF8.GetString(file.Content));
        Assert.Equal("ops@aeraigroup.com", file.Metadata["uploadedby"]);

        var batch = Assert.Single(_repository.Saved);
        Assert.Equal(path, batch.RawFilePath);
        Assert.Equal(64, batch.RawFileSha256!.Length);
    }

    [Fact]
    public async Task ImportAsync_UnparseableContent_KeepsRawFileButCreatesNoBatch()
    {
        var result = await CreateService().ImportAsync(Command("amazon-order-id,sku\n111-1,A-1\n"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("purchase-date", result.Error, StringComparison.Ordinal);
        Assert.Single(_rawFiles.Files);
        Assert.Empty(_repository.Saved);
    }

    [Theory]
    [InlineData("orders.xlsx")]
    [InlineData("orders.exe")]
    public async Task ImportAsync_DisallowedExtension_FailsBeforeStoringAnything(string fileName)
    {
        var result = await CreateService().ImportAsync(Command(OrdersCsv, fileName), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_rawFiles.Files);
        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task ImportAsync_FileLargerThanLimit_FailsBeforeStoringAnything()
    {
        var result = await CreateService(new ImportOptions { MaxFileBytes = 10 }).ImportAsync(Command(OrdersCsv), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_rawFiles.Files);
    }

    [Fact]
    public async Task ImportAsync_HeaderOnly_Fails()
    {
        var result = await CreateService().ImportAsync(
            Command("amazon-order-id,purchase-date,order-status,sku,quantity,item-price\n"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ImportAsync_FileNameWithPath_StoresFileNameOnly()
    {
        await CreateService().ImportAsync(Command(OrdersCsv, @"..\..\secret\orders.csv".Replace('\\', Path.DirectorySeparatorChar)), CancellationToken.None);

        Assert.Equal("orders.csv", Assert.Single(_repository.Saved).FileName);
        Assert.DoesNotContain("..", Assert.Single(_rawFiles.Files).Key, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_ReservedFileWithStagingInsteadOfTransfers_ReadsStagingAsTransfers()
    {
        // Header and quoting exactly as Amazon's live US report (October 2026).
        var content =
            "\"sku\",\"fnsku\",\"asin\",\"product-name\",\"reserved_qty\",\"reserved_customerorders\",\"reserved_fc-processing\",\"reserved_staging\",\"program\"\n" +
            "\"A-1\",\"X001\",\"B01\",\"Cloth, 3-Pack\",\"9\",\"2\",\"4\",\"3\",\"\"\n";

        var result = await CreateService().ImportAsync(
            Command(content, "reserved.csv", ImportSource.FbaReservedInventory), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        var row = Assert.Single(Assert.Single(_repository.Saved).FbaReservedRows);
        Assert.Equal("2", row.ReservedCustomerOrders);
        Assert.Equal("3", row.ReservedFcTransfers);
        Assert.Equal("4", row.ReservedFcProcessing);
    }

    [Fact]
    public async Task ImportAsync_ReservedFileWithDocumentedHeaders_ReadsTheColumns()
    {
        var content =
            "sku\treserved_qty\treserved_customerorders\treserved_fc-transfers\treserved_fc-processing\n" +
            "A-1\t9\t2\t3\t4\n";

        var result = await CreateService().ImportAsync(
            Command(content, "reserved.tsv", ImportSource.FbaReservedInventory), CancellationToken.None);

        Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        Assert.Equal("3", Assert.Single(Assert.Single(_repository.Saved).FbaReservedRows).ReservedFcTransfers);
    }

    [Fact]
    public async Task ImportAsync_RequiredColumnMissing_ErrorListsTheFilesColumns()
    {
        var content = "sku\treserved_customerorders\tmystery_column\treserved_fc-processing\nA-1\t2\t3\t4\n";

        var result = await CreateService().ImportAsync(
            Command(content, "reserved.tsv", ImportSource.FbaReservedInventory), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("reserved-fc-transfers", result.Error, StringComparison.Ordinal);
        Assert.Contains("mystery-column", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StageRawFileAsync_FileMissingFromStore_Fails()
    {
        var result = await CreateService().StageRawFileAsync(
            new StageRawFileCommand(ImportSource.Orders, MarketplaceIds.UnitedStates, "orders/2026/10/02/missing.csv", new string('0', 64), "missing.csv", "spapi-worker"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task RestageAsync_BatchWithRawFile_CreatesNewBatchFromStoredFile()
    {
        var service = CreateService();
        await service.ImportAsync(Command(OrdersCsv, marketplaceId: MarketplaceIds.Canada), CancellationToken.None);
        var original = _repository.Saved[0];
        _batches.Batches.Add(new ImportBatchSummary(original.Id, original.Source, original.MarketplaceId, original.FileName, original.UploadedBy, original.UploadedAt,
            original.Status, original.RowCount, 0, 0, null, null, original.RawFilePath, original.RawFileSha256));

        var result = await service.RestageAsync(original.Id, "admin@aeraigroup.com", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(original.Id, result.Value.BatchId);
        var restaged = _repository.Saved[1];
        Assert.Equal(original.RawFilePath, restaged.RawFilePath);
        Assert.Equal("admin@aeraigroup.com", restaged.UploadedBy);
        Assert.Equal(MarketplaceIds.Canada, restaged.MarketplaceId);
        Assert.Equal(original.OrderLines.Count, restaged.OrderLines.Count);
        Assert.Single(_rawFiles.Files); // Re-staging reads the stored file; it never writes a new one.
    }

    [Fact]
    public async Task RestageAsync_BatchWithoutRawFile_Fails()
    {
        _batches.Batches.Add(new ImportBatchSummary(7, ImportSource.Orders, MarketplaceIds.UnitedStates, "old.csv", "x", DateTimeOffset.UnixEpoch,
            ImportBatchStatus.Promoted, 1, 1, 0, null, null, null, null));

        var result = await CreateService().RestageAsync(7, "admin@aeraigroup.com", CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
