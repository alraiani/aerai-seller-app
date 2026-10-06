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

    private InventoryItemService CreateService() =>
        new(_repository, _images, _spreadsheets, new FakeTimeProvider(DateTimeOffset.UnixEpoch), NullLogger<InventoryItemService>.Instance);

    private static MemoryStream Text(string text) => new(Encoding.UTF8.GetBytes(text));

    [Fact]
    public async Task UpdateAsync_NewFamilyName_CreatesItTidiedAndReusesItCaseInsensitively()
    {
        var service = CreateService();

        await service.UpdateAsync("MAT-BLK", Us, "  Yoga   mats ", 5, User, CancellationToken.None);
        await service.UpdateAsync("MAT-BLU", Us, "yoga mats", 0, User, CancellationToken.None);

        var family = Assert.Single(_repository.Families);
        Assert.Equal("Yoga mats", family.Name);
        Assert.All(_repository.Products.Values, p => Assert.Equal(family.Id, p.FamilyId));
        Assert.Equal(5, _repository.HomeStock[(Us, "MAT-BLK")]);
    }

    [Fact]
    public async Task UpdateAsync_BlankFamily_ClearsAndRemovesUnusedFamily()
    {
        var service = CreateService();
        await service.UpdateAsync("MAT-BLK", Us, "Mats", 0, User, CancellationToken.None);

        var result = await service.UpdateAsync("MAT-BLK", Us, " ", 0, User, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(_repository.Products["MAT-BLK"].FamilyId);
        Assert.Empty(_repository.Families);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(InventoryItemService.MaxHomeStock + 1)]
    public async Task UpdateAsync_HomeStockOutOfRange_Fails(int homeStock)
    {
        var result = await CreateService().UpdateAsync("MAT-BLK", Us, null, homeStock, User, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(0, _repository.SaveCalls);
    }

    [Fact]
    public async Task UpdateAsync_UnknownSku_Fails()
    {
        var result = await CreateService().UpdateAsync("NOPE", Us, null, 1, User, CancellationToken.None);

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
    }

    [Fact]
    public async Task ImportHomeStockAsync_Csv_SavesValidRowsAndReportsTheRest()
    {
        const string csv =
            "SKU,Home Stock,product-name\n" +
            "MAT-BLK,10,Mat\n" +
            "MAT-BLU,\"1,200\",Mat\n" +
            "GHOST,5,Unknown\n" +
            "MAT-BLK,-3,Bad\n" +
            ",7,No sku\n" +
            "MAT-BLK,12,Last wins\n";

        var result = await CreateService().ImportHomeStockAsync(Us, "stock.csv", Text(csv), User, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(2, result.Value.Saved);
        Assert.Equal([3, 4, 5], result.Value.Rejected.Select(r => r.RowNumber));
        Assert.Equal(12, _repository.HomeStock[(Us, "MAT-BLK")]);
        Assert.Equal(1200, _repository.HomeStock[(Us, "MAT-BLU")]);
    }

    [Fact]
    public async Task ImportHomeStockAsync_MissingColumn_Fails()
    {
        var result = await CreateService().ImportHomeStockAsync(Us, "stock.csv", Text("sku,qty\nMAT-BLK,1\n"), User, CancellationToken.None);

        Assert.Contains("'home-stock'", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportHomeStockAsync_UnsupportedType_Fails()
    {
        var result = await CreateService().ImportHomeStockAsync(Us, "stock.xls", Text("x"), User, CancellationToken.None);

        Assert.Equal("Upload a .xlsx, .csv, or .tsv file.", result.Error);
    }

    [Fact]
    public async Task ImportHomeStockAsync_Xlsx_ReadsThroughTheSpreadsheetReader()
    {
        _spreadsheets.Sheet = new ParsedFile(["sku", "home-stock"],
            [new ParsedRecord(1, "MAT-BLU\t8", new Dictionary<string, string> { ["sku"] = "MAT-BLU", ["home-stock"] = "8" })]);

        var result = await CreateService().ImportHomeStockAsync(Us, "Stock.XLSX", new MemoryStream(), User, CancellationToken.None);

        Assert.True(_spreadsheets.WasCalled);
        Assert.Equal(1, result.Value.Saved);
        Assert.Equal(8, _repository.HomeStock[(Us, "MAT-BLU")]);
    }
}
