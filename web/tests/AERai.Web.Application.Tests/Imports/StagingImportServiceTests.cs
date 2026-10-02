using System.Text;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Tests.Fakes;
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
        [new OrderLineMapper(), new InventoryRowMapper(), new SettlementLineMapper()],
        _rawFiles,
        _repository,
        _batches,
        Options.Create(options ?? new ImportOptions()),
        _clock,
        NullLogger<StagingImportService>.Instance);

    private static ImportFileCommand Command(string content, string fileName = "orders.csv", ImportSource source = ImportSource.Orders)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new ImportFileCommand(source, fileName, bytes.Length, new MemoryStream(bytes), "ops@aeraigroup.com");
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
    public async Task StageRawFileAsync_FileMissingFromStore_Fails()
    {
        var result = await CreateService().StageRawFileAsync(
            new StageRawFileCommand(ImportSource.Orders, "orders/2026/10/02/missing.csv", new string('0', 64), "missing.csv", "spapi-worker"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_repository.Saved);
    }

    [Fact]
    public async Task RestageAsync_BatchWithRawFile_CreatesNewBatchFromStoredFile()
    {
        var service = CreateService();
        await service.ImportAsync(Command(OrdersCsv), CancellationToken.None);
        var original = _repository.Saved[0];
        _batches.Batches.Add(new ImportBatchSummary(original.Id, original.Source, original.FileName, original.UploadedBy, original.UploadedAt,
            original.Status, original.RowCount, 0, 0, null, null, original.RawFilePath, original.RawFileSha256));

        var result = await service.RestageAsync(original.Id, "admin@aeraigroup.com", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEqual(original.Id, result.Value.BatchId);
        var restaged = _repository.Saved[1];
        Assert.Equal(original.RawFilePath, restaged.RawFilePath);
        Assert.Equal("admin@aeraigroup.com", restaged.UploadedBy);
        Assert.Equal(original.OrderLines.Count, restaged.OrderLines.Count);
        Assert.Single(_rawFiles.Files); // Re-staging reads the stored file; it never writes a new one.
    }

    [Fact]
    public async Task RestageAsync_BatchWithoutRawFile_Fails()
    {
        _batches.Batches.Add(new ImportBatchSummary(7, ImportSource.Orders, "old.csv", "x", DateTimeOffset.UnixEpoch,
            ImportBatchStatus.Promoted, 1, 1, 0, null, null, null, null));

        var result = await CreateService().RestageAsync(7, "admin@aeraigroup.com", CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
