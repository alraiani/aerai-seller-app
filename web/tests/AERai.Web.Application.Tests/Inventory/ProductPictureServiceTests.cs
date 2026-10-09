using System.IO.Compression;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class ProductPictureServiceTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private readonly FakeInventoryItemRepository _repository = new();
    private readonly FakeProductImageStore _images = new();
    private readonly FakeAmazonCatalogGateway _catalog = new();
    private readonly Marketplace _us = TestMarketplaces.UnitedStates;

    public ProductPictureServiceTests()
    {
        Add("MAT-BLK", "B0MAT00001");
        Add("MAT-BLK-FBM", "B0MAT00001");
        Add("MAT-BLU", "B0MAT00002");
        Add("STRAP", null);
    }

    private void Add(string sku, string? asin) => _repository.Products[sku] = new Product { Sku = sku, Asin = asin };

    private ProductPictureService CreateService(bool connected = true)
    {
        var items = new InventoryItemService(_repository, _images, new FakeSpreadsheetReader(), new FakeTimeProvider(DateTimeOffset.UnixEpoch), NullLogger<InventoryItemService>.Instance);
        return new ProductPictureService(_repository, items, _catalog, new FakeConnection(connected), NullLogger<ProductPictureService>.Instance);
    }

    private string? PictureOf(string sku) => _repository.Products[sku].ImagePath;

    private static PictureFile File(string name, byte[] bytes) => new(name, new MemoryStream(bytes));

    private static PictureFile Zip(string name, params (string Entry, byte[] Bytes)[] entries)
    {
        var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entry, bytes) in entries)
            {
                using var stream = archive.CreateEntry(entry).Open();
                stream.Write(bytes);
            }
        }

        buffer.Position = 0;
        return new PictureFile(name, buffer);
    }

    [Fact]
    public async Task GetCoverageAsync_CountsPicturesAndWhatAmazonCanFill()
    {
        _repository.Products["MAT-BLU"].ImagePath = "x.png";

        var coverage = await CreateService().GetCoverageAsync(CancellationToken.None);

        Assert.Equal(new PictureCoverage(4, 1, 2, 1), coverage);
    }

    [Fact]
    public async Task PullFromAmazonAsync_SkusSharingAnAsin_DownloadOnceAndBothGetIt()
    {
        _catalog.Pictures["B0MAT00001"] = Png;
        _catalog.Pictures["B0MAT00002"] = Jpeg;

        var result = await CreateService().PullFromAmazonAsync(_us, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.StoredSkus);
        Assert.Equal(2, _catalog.Downloads);
        Assert.NotNull(PictureOf("MAT-BLK"));
        Assert.NotNull(PictureOf("MAT-BLK-FBM"));
        Assert.NotEqual(PictureOf("MAT-BLK"), PictureOf("MAT-BLK-FBM"));
        Assert.Equal("image/jpeg", _repository.Products["MAT-BLU"].ImageContentType);
        Assert.Equal(1, result.Value.WithoutAsin);
    }

    [Fact]
    public async Task PullFromAmazonAsync_SkuWithAPicture_IsNeverLookedUpOrReplaced()
    {
        _repository.Products["MAT-BLU"].ImagePath = "mine.png";
        _repository.Products["MAT-BLU"].ImageContentType = "image/png";
        _catalog.Pictures["B0MAT00001"] = Png;
        _catalog.Pictures["B0MAT00002"] = Jpeg;

        await CreateService().PullFromAmazonAsync(_us, CancellationToken.None);

        Assert.DoesNotContain("B0MAT00002", _catalog.LookedUp);
        Assert.Equal("mine.png", PictureOf("MAT-BLU"));
    }

    [Fact]
    public async Task PullFromAmazonAsync_AsinWithoutAPictureOrFailingDownload_IsReportedAndTheRestStored()
    {
        _catalog.Pictures["B0MAT00001"] = Png;
        _catalog.FailingDownloads.Add("B0MAT00001");

        var result = await CreateService().PullFromAmazonAsync(_us, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var failed = Assert.Single(result.Value.Outcomes, o => o.Status == PictureImportStatus.Failed);
        Assert.Equal(["MAT-BLK", "MAT-BLK-FBM"], failed.Skus);
        var missing = Assert.Single(result.Value.Outcomes, o => o.Status == PictureImportStatus.NotOnAmazon);
        Assert.Equal("B0MAT00002", missing.Source);
        Assert.Null(PictureOf("MAT-BLK"));
    }

    [Fact]
    public async Task PullFromAmazonAsync_AmazonPictureNotAnImage_IsReportedAsFailed()
    {
        _catalog.Pictures["B0MAT00002"] = "<html>not a picture</html>"u8.ToArray();

        var result = await CreateService().PullFromAmazonAsync(_us, CancellationToken.None);

        var failed = Assert.Single(result.Value.Outcomes, o => o.Status == PictureImportStatus.Failed);
        Assert.Equal(["MAT-BLU"], failed.Skus);
        Assert.Contains("JPEG, PNG, or WebP", failed.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PullFromAmazonAsync_NotConnected_FailsWithoutCallingAmazon()
    {
        var result = await CreateService(connected: false).PullFromAmazonAsync(_us, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("isn't connected", result.Error, StringComparison.Ordinal);
        Assert.Empty(_catalog.LookedUp);
    }

    [Fact]
    public async Task PullFromAmazonAsync_CatalogLookupFails_ReturnsTheReason()
    {
        _catalog.LookupFailure = new HttpRequestException("SP-API catalog.searchCatalogItems failed with HTTP 403");

        var result = await CreateService().PullFromAmazonAsync(_us, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("HTTP 403", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PullFromAmazonAsync_MoreThanOnePullsWorth_StopsAtTheCapAndCountsTheRest()
    {
        _repository.Products.Clear();
        for (var i = 0; i < ProductPictureService.MaxAmazonDownloadsPerPull + 3; i++)
        {
            var asin = $"B0{i:D8}";
            Add($"SKU-{i:D4}", asin);
            _catalog.Pictures[asin] = Png;
        }

        var service = CreateService();
        var first = await service.PullFromAmazonAsync(_us, CancellationToken.None);
        var second = await service.PullFromAmazonAsync(_us, CancellationToken.None);

        Assert.Equal(ProductPictureService.MaxAmazonDownloadsPerPull, first.Value.StoredSkus);
        Assert.Equal(3, first.Value.Remaining);
        Assert.Equal(3, second.Value.StoredSkus);
        Assert.Equal(0, second.Value.Remaining);
    }

    [Fact]
    public async Task UploadAsync_FileNames_MatchSkuExactlyThenIgnoringCaseThenAsin()
    {
        var result = await CreateService().UploadAsync(
            [File("STRAP.jpg", Jpeg), File("mat-blu.PNG", Png), File("b0mat00001.png", Png)], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Outcomes, o => Assert.Equal(PictureImportStatus.Added, o.Status));
        Assert.Equal(4, result.Value.StoredSkus);
        Assert.Equal(["MAT-BLK", "MAT-BLK-FBM"], result.Value.Outcomes[2].Skus);
        Assert.Equal("image/jpeg", _repository.Products["STRAP"].ImageContentType);
    }

    [Fact]
    public async Task UploadAsync_SkusDifferingOnlyByCase_AreNotGuessed()
    {
        Add("strap", null);

        var result = await CreateService().UploadAsync([File("Strap.png", Png)], CancellationToken.None);

        var outcome = Assert.Single(result.Value.Outcomes);
        Assert.Equal(PictureImportStatus.NoMatch, outcome.Status);
        Assert.Contains("upper/lower case", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UploadAsync_ExistingPicture_IsReplacedAndTheOldBlobDeleted()
    {
        var service = CreateService();
        await service.UploadAsync([File("STRAP.png", Png)], CancellationToken.None);
        var old = PictureOf("STRAP")!;

        var result = await service.UploadAsync([File("STRAP.jpg", Jpeg)], CancellationToken.None);

        Assert.Equal(PictureImportStatus.Replaced, Assert.Single(result.Value.Outcomes).Status);
        Assert.False(_images.Blobs.ContainsKey(old));
        Assert.Equal("image/jpeg", _repository.Products["STRAP"].ImageContentType);
    }

    [Fact]
    public async Task UploadAsync_Zip_StoresEntriesInFoldersAndSkipsArchiveClutter()
    {
        var zip = Zip(
            "pictures.zip",
            ("blue/MAT-BLU.png", Png),
            ("STRAP.jpg", Jpeg),
            ("__MACOSX/blue/._MAT-BLU.png", [0x00, 0x05]),
            (".DS_Store", [0x00]));

        var result = await CreateService().UploadAsync([zip], CancellationToken.None);

        Assert.Equal(2, result.Value.Outcomes.Count);
        Assert.Equal("pictures.zip › blue/MAT-BLU.png", result.Value.Outcomes[0].Source);
        Assert.NotNull(PictureOf("MAT-BLU"));
        Assert.NotNull(PictureOf("STRAP"));
    }

    [Fact]
    public async Task UploadAsync_ProblemFiles_AreListedAndSkipped()
    {
        var result = await CreateService().UploadAsync(
            [
                File("GHOST.png", Png),
                File("STRAP.gif", Png),
                File("MAT-BLU.png", "GIF89a"u8.ToArray()),
                File("STRAP.png", Png),
                File("strap.jpg", Jpeg),
                new PictureFile("not-really.zip", new MemoryStream([1, 2, 3])),
            ],
            CancellationToken.None);

        var statuses = result.Value.Outcomes.ToDictionary(o => o.Source, o => o.Status);
        Assert.Equal(PictureImportStatus.NoMatch, statuses["GHOST.png"]);
        Assert.Equal(PictureImportStatus.Rejected, statuses["STRAP.gif"]);
        Assert.Equal(PictureImportStatus.Rejected, statuses["MAT-BLU.png"]);
        Assert.Equal(PictureImportStatus.Added, statuses["STRAP.png"]);
        Assert.Equal(PictureImportStatus.Duplicate, statuses["strap.jpg"]);
        Assert.Equal(PictureImportStatus.Rejected, statuses["not-really.zip"]);
        Assert.Equal("image/png", _repository.Products["STRAP"].ImageContentType);
        Assert.Null(PictureOf("MAT-BLU"));
    }

    [Fact]
    public async Task UploadAsync_ZipEntryLargerThanTheLimit_IsRejectedWithoutStoringIt()
    {
        var huge = new byte[InventoryItemService.MaxImageBytes + 1];
        Png.CopyTo(huge, 0);

        var result = await CreateService().UploadAsync([Zip("big.zip", ("STRAP.png", huge))], CancellationToken.None);

        var outcome = Assert.Single(result.Value.Outcomes);
        Assert.Equal(PictureImportStatus.Rejected, outcome.Status);
        Assert.Empty(_images.Blobs);
    }

    [Fact]
    public async Task UploadAsync_TooManyPictures_RefusesTheWholeUpload()
    {
        var entries = Enumerable.Range(0, ProductPictureService.MaxFilesPerUpload + 1).Select(i => ($"P{i}.png", Png)).ToArray();

        var result = await CreateService().UploadAsync([Zip("many.zip", entries)], CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_images.Blobs);
    }

    [Fact]
    public async Task UploadAsync_NoFiles_Fails()
    {
        var result = await CreateService().UploadAsync([], CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
