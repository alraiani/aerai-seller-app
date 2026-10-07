using System.Text;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class InventoryItemServiceTests
{
    private const string Us = MarketplaceIds.UnitedStates;
    private const string User = "ops@aeraigroup.com";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10];

    private readonly FakeInventoryItemRepository _repository = new();
    private readonly FakeProductImageStore _images = new();
    private readonly FakeSpreadsheetReader _spreadsheets = new();

    public InventoryItemServiceTests()
    {
        _repository.AddProduct("MAT-BLK");
        _repository.AddProduct("MAT-BLU");
    }

    private readonly FakeStockAlertRefreshSignal _alerts = new();

    private InventoryItemService CreateService() =>
        new(_repository, _images, _spreadsheets, _alerts, new FakeTimeProvider(DateTimeOffset.UnixEpoch), NullLogger<InventoryItemService>.Instance);

    private static MemoryStream Text(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task UpdateAsync_NewFamilyName_CreatesItTidiedAndReusesItCaseInsensitively()
    {
        var service = CreateService();

        await service.UpdateAsync("MAT-BLK", Us, new InventoryItemUpdate("  Yoga   mats ", 5, LeadTimeSettings.None), User, CancellationToken.None);
        await service.UpdateAsync("MAT-BLU", Us, new InventoryItemUpdate("yoga mats", 0, LeadTimeSettings.None), User, CancellationToken.None);

        var family = Assert.Single(_repository.Families);
        Assert.Equal("Yoga mats", family.Name);
        Assert.All(_repository.Products.Values, p => Assert.Equal(family.Id, p.FamilyId));
        Assert.Equal(5, _repository.HomeStock[(Us, "MAT-BLK")]);
    }

    [Fact]
    public async Task UpdateAsync_BlankFamily_ClearsTheSkuButKeepsTheFamily()
    {
        var service = CreateService();
        await service.UpdateAsync("MAT-BLK", Us, new InventoryItemUpdate("Mats", 0, LeadTimeSettings.None), User, CancellationToken.None);

        var result = await service.UpdateAsync("MAT-BLK", Us, new InventoryItemUpdate(" ", 0, LeadTimeSettings.None), User, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(_repository.Products["MAT-BLK"].FamilyId);
        Assert.Single(_repository.Families); // families are managed explicitly, not deleted when empty
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(InventoryItemService.MaxHomeStock + 1)]
    public async Task UpdateAsync_HomeStockOutOfRange_Fails(int homeStock)
    {
        var result = await CreateService().UpdateAsync("MAT-BLK", Us, new InventoryItemUpdate(null, homeStock, LeadTimeSettings.None), User, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, _repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateAsync_LeadTimes_SavedPerMarketplaceAndBlankRemovesThem()
    {
        var service = CreateService();
        var custom = new LeadTimeSettings(45, null, 12, null, 120);

        await service.UpdateAsync("MAT-BLK", Us, new InventoryItemUpdate(null, 0, custom), User, CancellationToken.None);
        Assert.Equal(custom, _repository.LeadTimes[(Us, "MAT-BLK")]);
        Assert.False(_repository.LeadTimes.ContainsKey((MarketplaceIds.Canada, "MAT-BLK")));

        await service.UpdateAsync("MAT-BLK", Us, new InventoryItemUpdate(null, 0, LeadTimeSettings.None), User, CancellationToken.None);
        Assert.Empty(_repository.LeadTimes);
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(InventoryItemService.MaxLeadTimeDays + 1, null)]
    [InlineData(null, 0)]
    public async Task UpdateAsync_LeadTimeOutOfRange_FailsAndSavesNothing(int? supplierDays, int? targetDays)
    {
        var update = new InventoryItemUpdate("Mats", 5, new LeadTimeSettings(supplierDays, null, null, null, targetDays));

        var result = await CreateService().UpdateAsync("MAT-BLK", Us, update, User, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_repository.Families);
        Assert.Equal(0, _repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateAsync_UnknownSku_Fails()
    {
        var result = await CreateService().UpdateAsync("NOPE", Us, new InventoryItemUpdate(null, 1, LeadTimeSettings.None), User, CancellationToken.None);

        Assert.Equal("Product 'NOPE' was not found.", result.Error);
    }

    [Fact]
    public async Task SetImageAsync_ValidPicture_StoresItAndDeletesThePreviousOne()
    {
        var service = CreateService();
        await service.SetImageAsync("MAT-BLK", new MemoryStream(Png), Png.Length, CancellationToken.None);
        var first = _repository.Products["MAT-BLK"].ImagePath;

        var result = await service.SetImageAsync("MAT-BLK", new MemoryStream(Jpeg), Jpeg.Length, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var (path, blob) = Assert.Single(_images.Blobs);
        Assert.NotEqual(first, path);
        Assert.Equal("image/jpeg", blob.ContentType);
        Assert.Equal(path, _repository.Products["MAT-BLK"].ImagePath);
    }

    [Fact]
    public async Task SetImageAsync_NotJpegPngOrWebp_IsRejectedWhateverItClaims()
    {
        byte[] gif = "GIF89a..."u8.ToArray();

        var result = await CreateService().SetImageAsync("MAT-BLK", new MemoryStream(gif), gif.Length, CancellationToken.None);

        Assert.Equal("Only JPEG, PNG, or WebP pictures are accepted.", result.Error);
        Assert.Empty(_images.Blobs);
    }

    [Fact]
    public async Task SetImageAsync_TooLarge_IsRejected()
    {
        var result = await CreateService().SetImageAsync("MAT-BLK", new MemoryStream(Png), InventoryItemService.MaxImageBytes + 1, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_images.Blobs);
    }

    [Fact]
    public async Task RemoveImageAsync_DeletesBlobAndClearsProduct()
    {
        var service = CreateService();
        await service.SetImageAsync("MAT-BLK", new MemoryStream(Png), Png.Length, CancellationToken.None);

        await service.RemoveImageAsync("MAT-BLK", CancellationToken.None);

        Assert.Empty(_images.Blobs);
        Assert.Null(await service.OpenImageAsync("MAT-BLK", CancellationToken.None));
    }

    [Fact]
    public async Task SetHomeStockAsync_UnknownSku_FailsAndSavesNothing()
    {
        var result = await CreateService().SetHomeStockAsync(Us, [new("MAT-BLK", 4), new("GHOST", 2)], User, CancellationToken.None);

        Assert.Equal("Product 'GHOST' was not found.", result.Error);
        Assert.Empty(_repository.HomeStock);
        Assert.Equal(0, _alerts.Requests);
    }

    [Fact]
    public async Task SetHomeStockAsync_ZeroClearsAndOtherMarketplacesAreUntouched()
    {
        _repository.HomeStock[(Us, "MAT-BLK")] = 9;
        _repository.HomeStock[(MarketplaceIds.Canada, "MAT-BLK")] = 3;

        var result = await CreateService().SetHomeStockAsync(Us, [new("MAT-BLK", 0), new("MAT-BLU", 12)], User, CancellationToken.None);

        Assert.Equal(2, result.Value);
        Assert.False(_repository.HomeStock.ContainsKey((Us, "MAT-BLK")));
        Assert.Equal(3, _repository.HomeStock[(MarketplaceIds.Canada, "MAT-BLK")]);
        Assert.Equal(12, _repository.HomeStock[(Us, "MAT-BLU")]);
        Assert.Equal(1, _alerts.Requests); // Home stock drives reorder timing, so alerts must be re-evaluated.
    }

    [Fact]
    public async Task PreviewHomeStockImportAsync_ComparesWithHomeStockAndSavesNothing()
    {
        _repository.HomeStock[(Us, "MAT-BLK")] = 20;
        _repository.AddProduct("STRAP");
        _repository.HomeStock[(Us, "STRAP")] = 4;
        const string csv =
            "SKU,Home Stock,product-name\n" +
            "MAT-BLK,10,Mat\n" +
            "MAT-BLU,\"1,200\",Mat\n" +
            "GHOST,5,Unknown\n" +
            "MAT-BLK,-3,Bad\n" +
            ",7,No sku\n" +
            "STRAP,4,Same\n" +
            "MAT-BLK,12,Last wins\n";

        var result = await CreateService().PreviewHomeStockImportAsync(Us, "stock.csv", Text(csv), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var preview = result.Value;
        Assert.Equal([("MAT-BLU", 0, 1200), ("MAT-BLK", 20, 12)], preview.Changes.Select(c => (c.Sku, c.Current, c.New)));
        Assert.Equal((1, 1, 1200, 8, 1), (preview.IncreaseCount, preview.DecreaseCount, preview.UnitsIn, preview.UnitsOut, preview.UnchangedCount));
        Assert.Equal([3, 4, 5], preview.Rejected.Select(r => r.RowNumber));
        Assert.Equal(20, _repository.HomeStock[(Us, "MAT-BLK")]);
        Assert.False(_repository.HomeStock.ContainsKey((Us, "MAT-BLU")));
    }

    [Fact]
    public async Task PreviewHomeStockImportAsync_MissingColumn_Fails()
    {
        var result = await CreateService().PreviewHomeStockImportAsync(Us, "stock.csv", Text("sku,qty\nMAT-BLK,1\n"), CancellationToken.None);

        Assert.Contains("'home-stock'", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewHomeStockImportAsync_UnsupportedType_Fails()
    {
        var result = await CreateService().PreviewHomeStockImportAsync(Us, "stock.xls", Text("x"), CancellationToken.None);

        Assert.Equal("Upload a .xlsx, .csv, or .tsv file.", result.Error);
    }

    [Fact]
    public async Task PreviewHomeStockImportAsync_Xlsx_ReadsThroughTheSpreadsheetReader()
    {
        _spreadsheets.Sheet = new ParsedFile(["sku", "home-stock"],
            [new ParsedRecord(1, "MAT-BLU\t8", new Dictionary<string, string> { ["sku"] = "MAT-BLU", ["home-stock"] = "8" })]);

        var result = await CreateService().PreviewHomeStockImportAsync(Us, "Stock.XLSX", new MemoryStream(), CancellationToken.None);

        Assert.True(_spreadsheets.WasCalled);
        Assert.Equal(8, Assert.Single(result.Value.Changes).Difference);
    }
}
