using AERai.Web.Application.Products;
using AERai.Web.Infrastructure.Spreadsheets;
using ClosedXML.Excel;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>The downloadable COGS sheet must upload back unchanged (no external services).</summary>
public sealed class ClosedXmlProductCostTemplateWriterTests
{
    private static readonly ProductCostTemplateRow[] Rows =
    [
        new("MAT-BLK", 4.5m, "B000000001", "=Yoga mat, Black", "Mats", new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero)),
        new("MAT-BLU", null, null, "Yoga mat, Blue", "Mats", null),
    ];

    [Fact]
    public async Task Write_ReadsBackWithTheUploadsColumnsAndCosts()
    {
        var bytes = new ClosedXmlProductCostTemplateWriter().Write("Cost of goods — United States (USD)", "USD", Rows);

        using var stream = new MemoryStream(bytes);
        var parsed = await new ClosedXmlSpreadsheetReader().ReadAsync(stream, 100, CancellationToken.None);

        Assert.True(parsed.IsSuccess, parsed.Error);
        Assert.Equal(ProductService.CostSheetColumns, parsed.Value.Headers.Take(2));
        var costs = parsed.Value.Records.Select(r => (r.Get("sku"), ProductCostText.TryParse(r.Get("cost-of-goods"), out var c) ? c : -1)).ToList();
        Assert.Equal([("MAT-BLK", (decimal?)4.50m), ("MAT-BLU", null)], costs);
    }

    [Fact]
    public void Write_StoresTitlesAsTextNeverFormulas()
    {
        using var workbook = new XLWorkbook(new MemoryStream(new ClosedXmlProductCostTemplateWriter().Write("Cost of goods", "USD", Rows)));
        var cell = workbook.Worksheet("Cost of goods").Cell(2, 4);

        Assert.False(cell.HasFormula);
        Assert.Equal("=Yoga mat, Black", cell.GetString());
        Assert.Equal("How to use", workbook.Worksheet(2).Name);
    }
}
