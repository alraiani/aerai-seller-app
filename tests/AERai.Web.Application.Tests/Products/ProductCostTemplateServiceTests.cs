using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Products;
using AERai.Web.Application.Tests.Fakes;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Products;

public sealed class ProductCostTemplateServiceTests
{
    private readonly FakeProductRepository _repository = new();
    private readonly RecordingWriter _writer = new();

    // 02:00 UTC is still the previous day in New York, so the file name must use the local date.
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 3, 2, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task CreateAsync_AllProducts_NamesFileByMarketplaceAndLocalDate()
    {
        _repository.SheetRows.Add(new ProductCostTemplateRow("A-1", 4m, null, "Mat", "Mats", null));

        var (fileName, _) = await new ProductCostTemplateService(_repository, _writer, _clock).CreateAsync(TestMarketplaces.UnitedStates, null, CancellationToken.None);

        Assert.Equal("cogs-US-2026-10-02.xlsx", fileName);
        Assert.Equal(("Cost of goods — United States (USD)", "USD", 1), (_writer.Title, _writer.Currency, _writer.Rows!.Count));
    }

    [Fact]
    public async Task CreateAsync_OneFamily_AddsItsSlugToTheName()
    {
        _repository.SheetRows.Add(new ProductCostTemplateRow("A-1", null, null, null, "Yoga Mats", null));

        var (fileName, _) = await new ProductCostTemplateService(_repository, _writer, _clock).CreateAsync(TestMarketplaces.Canada, 7, CancellationToken.None);

        Assert.Equal("cogs-CA-yoga-mats-2026-10-02.xlsx", fileName);
        Assert.Equal("Cost of goods — Yoga Mats, Canada (CAD)", _writer.Title);
    }

    private sealed class RecordingWriter : IProductCostTemplateWriter
    {
        public string? Title { get; private set; }

        public string? Currency { get; private set; }

        public IReadOnlyList<ProductCostTemplateRow>? Rows { get; private set; }

        public byte[] Write(string title, string currency, IReadOnlyList<ProductCostTemplateRow> rows)
        {
            (Title, Currency, Rows) = (title, currency, rows);
            return [];
        }
    }
}
