using AERai.Web.Infrastructure.Spreadsheets;
using ClosedXML.Excel;

namespace AERai.Web.Infrastructure.Tests;

/// <summary>Tests the Excel reader with workbooks built in memory (no external services).</summary>
public sealed class ClosedXmlSpreadsheetReaderTests
{
    private static MemoryStream Workbook(Action<IXLWorksheet> fill)
    {
        using var workbook = new XLWorkbook();
        fill(workbook.AddWorksheet("Stock"));
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public async Task ReadAsync_NormalizesHeadersAndReadsNumbersByValue()
    {
        using var file = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "SKU";
            sheet.Cell(1, 2).Value = "Home Stock";
            sheet.Cell(2, 1).Value = "MAT-BLK";
            sheet.Cell(2, 2).Value = 1200;
            sheet.Cell(2, 2).Style.NumberFormat.Format = "#,##0.00"; // displayed as "1,200.00"
            sheet.Cell(4, 1).Value = "MAT-BLU";                         // row 3 left empty
            sheet.Cell(4, 2).Value = "7";
        });

        var result = await new ClosedXmlSpreadsheetReader().ReadAsync(file, 100, CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(["sku", "home-stock"], result.Value.Headers);
        Assert.Equal(["1200", "7"], result.Value.Records.Select(r => r.Get("home-stock")));
        Assert.Equal([1, 3], result.Value.Records.Select(r => r.RowNumber));
    }

    [Fact]
    public async Task ReadAsync_TooManyRows_Fails()
    {
        using var file = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "sku";
            for (var row = 2; row <= 5; row++)
            {
                sheet.Cell(row, 1).Value = $"S{row}";
            }
        });

        var result = await new ClosedXmlSpreadsheetReader().ReadAsync(file, 3, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ReadAsync_NotAWorkbook_FailsCleanly()
    {
        var result = await new ClosedXmlSpreadsheetReader().ReadAsync(new MemoryStream("sku,home-stock\n"u8.ToArray()), 10, CancellationToken.None);

        Assert.Equal("The file is not a readable Excel workbook (.xlsx).", result.Error);
    }

    [Fact]
    public async Task ReadAsync_SparseSheetFarBeyondTheRowCap_FailsWithoutReadingEveryRow()
    {
        using var file = Workbook(sheet =>
        {
            sheet.Cell(1, 1).Value = "sku";
            sheet.Cell(1_000_000, 1).Value = "far away";
        });

        var result = await new ClosedXmlSpreadsheetReader().ReadAsync(file, 10_000, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task ReadAsync_TooManyColumns_Fails()
    {
        using var file = Workbook(sheet => sheet.Cell(1, ClosedXmlSpreadsheetReader.MaxColumns + 1).Value = "x");

        var result = await new ClosedXmlSpreadsheetReader().ReadAsync(file, 10, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
