using AERai.Web.Infrastructure.SpApi;

namespace AERai.Web.Infrastructure.Tests.SpApi;

public sealed class CatalogGatewayTests
{
    private const string Us = "ATVPDKIKX0DER";
    private const string Ca = "A2EUQ1WTGCTBG2";

    private static CatalogImage Image(string variant, int size, string host = "m.media-amazon.com") =>
        new(variant, new Uri($"https://{host}/images/I/{variant}-{size}.jpg"), size, size);

    [Fact]
    public void PickMainImage_SeveralSizes_TakesTheLargestWithinTheLimit()
    {
        var item = new CatalogItem("B0TEST0001", [new(Us, [Image("PT01", 500), Image("MAIN", 75), Image("MAIN", 500), Image("MAIN", 1000), Image("MAIN", 2000)])]);

        Assert.Equal("/images/I/MAIN-1000.jpg", SpApiCatalogGateway.PickMainImage(item, Us)?.AbsolutePath);
    }

    [Fact]
    public void PickMainImage_EverySizeTooLarge_TakesTheSmallest()
    {
        var item = new CatalogItem("B0TEST0001", [new(Us, [Image("MAIN", 3000), Image("MAIN", 1500)])]);

        Assert.Equal("/images/I/MAIN-1500.jpg", SpApiCatalogGateway.PickMainImage(item, Us)?.AbsolutePath);
    }

    [Fact]
    public void PickMainImage_NoneInTheMarketplace_FallsBackToAnother()
    {
        var item = new CatalogItem("B0TEST0001", [new(Us, [Image("PT01", 500)]), new(Ca, [Image("MAIN", 500)])]);

        Assert.Equal("/images/I/MAIN-500.jpg", SpApiCatalogGateway.PickMainImage(item, Us)?.AbsolutePath);
    }

    [Fact]
    public void PickMainImage_NonAmazonHostOrNoMain_ReturnsNull()
    {
        Assert.Null(SpApiCatalogGateway.PickMainImage(new CatalogItem("B0TEST0001", [new(Us, [Image("MAIN", 500, "evil.example.com")])]), Us));
        Assert.Null(SpApiCatalogGateway.PickMainImage(new CatalogItem("B0TEST0001", null), Us));
    }

    [Fact]
    public async Task OpenImageAsync_NonAmazonHost_IsRefusedBeforeAnyRequest()
    {
        var gateway = new SpApiCatalogGateway(null!, null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.OpenImageAsync(new Uri("https://169.254.169.254/latest"), CancellationToken.None));
    }

    [Fact]
    public async Task SimulatedGateway_ProducesAValidPngForMostAsins()
    {
        var gateway = new SimulatedCatalogGateway();
        var asins = Enumerable.Range(0, 30).Select(i => $"B0SIM{i:D5}").ToList();

        var found = await gateway.FindMainImagesAsync(null!, asins, CancellationToken.None);
        await using var picture = await gateway.OpenImageAsync(found.Values.First(), CancellationToken.None);
        var bytes = new byte[8];
        await picture.ReadExactlyAsync(bytes);

        Assert.InRange(found.Count, 20, 29);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes);
    }
}
