using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Spreadsheets;
using ClosedXML.Excel;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>The downloadable home-stock sheet must upload back unchanged (no external services).</summary>
public sealed class ClosedXmlHomeStockTemplateWriterTests
{
    private static readonly HomeStockTemplateRow[] Rows =
    [
        new("FOB00BL", 75, "=Handkerchief, Blue", "Handkerchiefs", ProductColor.Blue, 24, 0, 10, null),
        new("FOB00GR", 44, "Handkerchief, Green", "Handkerchiefs", ProductColor.Green, 19, 5, 13, 12),
    ];

    [Fact]
    public async Task Write_ReadsBackWithTheUploadsColumnsAndValues()
    {
        var bytes = new ClosedXmlHomeStockTemplateWriter().Write("Home stock — United States", Rows);

        using var stream = new MemoryStream(bytes);
        var parsed = await new ClosedXmlSpreadsheetReader().ReadAsync(stream, 100, CancellationToken.None);

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(["sku", "home-stock"], parsed.Value.Headers.Take(2));
        Assert.Equal([("FOB00BL", "75"), ("FOB00GR", "44")], parsed.Value.Records.Select(r => (r.Get("sku"), r.Get("home-stock"))));
    }

    [Fact]
    public void Write_StoresTitlesAsTextNeverFormulas()
    {
        using var workbook = new XLWorkbook(new MemoryStream(new ClosedXmlHomeStockTemplateWriter().Write("Home stock", Rows)));
        var cell = workbook.Worksheet("Home stock").Cell(2, 3);

        Assert.False(cell.HasFormula);
        Assert.Equal("=Handkerchief, Blue", cell.GetString());
        Assert.Equal("How to use", workbook.Worksheet(2).Name);
    }
}
