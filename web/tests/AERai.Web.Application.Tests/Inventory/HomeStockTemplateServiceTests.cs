using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Reporting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class HomeStockTemplateServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 15, 0, 0, TimeSpan.Zero);

    private readonly FakeInventoryQueries _queries = new();
    private readonly CapturingWriter _writer = new();

    private HomeStockTemplateService CreateService()
    {
        var clock = new FakeTimeProvider(Now);
        return new(new InventoryService(_queries, Options.Create(new InventoryOptions()), clock), _writer, clock);
    }

    private void Stock(string sku, int? familyId, string? family, ProductColor? color, int home = 0) =>
        _queries.Positions.Add(new InventoryPosition
        {
            MarketplaceId = TestMarketplaces.UnitedStates.MarketplaceId, Sku = sku, SnapshotDate = new DateOnly(2026, 10, 7),
            FamilyId = familyId, Family = family, Color = color, HomeStock = home,
        });

    [Fact]
    public async Task CreateAsync_OrdersByFamilyThenColorThenSku()
    {
        Stock("MAT-1", 2, "Mats", null);
        Stock("HTS00PU", 1, "Handkerchiefs", ProductColor.Purple);
        Stock("FOB00GR", 1, "Handkerchiefs", ProductColor.Green, home: 44);
        Stock("FOB00PU", 1, "Handkerchiefs", ProductColor.Purple);
        Stock("LOOSE", null, null, null);

        var (fileName, _) = await CreateService().CreateAsync(TestMarketplaces.UnitedStates, null, CancellationToken.None);

        Assert.Equal(["FOB00GR", "FOB00PU", "HTS00PU", "MAT-1", "LOOSE"], _writer.Rows.Select(r => r.Sku));
        Assert.Equal(44, _writer.Rows[0].HomeStock);
        Assert.Equal("home-stock-US-2026-10-07.xlsx", fileName);
    }

    [Fact]
    public async Task CreateAsync_OneFamily_OnlyItsSkusAndNamedFile()
    {
        Stock("MAT-1", 2, "Microfiber Magic", null);
        Stock("FOB00GR", 1, "Handkerchiefs", ProductColor.Green);

        var (fileName, _) = await CreateService().CreateAsync(TestMarketplaces.UnitedStates, 2, CancellationToken.None);

        Assert.Equal(["MAT-1"], _writer.Rows.Select(r => r.Sku));
        Assert.Equal("home-stock-US-microfiber-magic-2026-10-07.xlsx", fileName);
        Assert.Equal("Home stock — Microfiber Magic, United States", _writer.Title);
    }

    private sealed class CapturingWriter : IHomeStockTemplateWriter
    {
        public IReadOnlyList<HomeStockTemplateRow> Rows { get; private set; } = [];

        public string Title { get; private set; } = string.Empty;

        public byte[] Write(string title, IReadOnlyList<HomeStockTemplateRow> rows)
        {
            (Title, Rows) = (title, rows);
            return [];
        }
    }
}
