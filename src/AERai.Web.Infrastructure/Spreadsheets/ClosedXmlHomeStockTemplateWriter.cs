using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using ClosedXML.Excel;

namespace AERai.Web.Infrastructure.Spreadsheets;

/// <summary>
/// ClosedXML implementation of <see cref="IHomeStockTemplateWriter"/>. The first sheet is exactly
/// what the upload reads (header row with <c>sku</c> and an empty <c>change</c> column for units added
/// or removed); the other columns, current home stock first, are reference only and ignored on upload.
/// A second sheet explains how to fill it in.
/// </summary>
internal sealed class ClosedXmlHomeStockTemplateWriter : IHomeStockTemplateWriter
{
    /// <summary>The two columns the upload reads; their header text must stay exactly this.</summary>
    internal static readonly string[] MappedHeaders = ["sku", "change"];

    /// <summary>Reference columns, ignored on upload.</summary>
    private static readonly string[] ReferenceHeaders = ["At home", "Product name", "Family", "Color", "At Amazon", "Inbound", "Sold 30 days", "Send to Amazon"];

    /// <summary>
    /// Text colors for each product color, matching the light theme's palette in site.css so the
    /// sheet reads like the on-screen worksheet.
    /// </summary>
    private static readonly Dictionary<ProductColor, string> ColorHex = new()
    {
        [ProductColor.Black] = "#1F2937", [ProductColor.White] = "#94A3B8", [ProductColor.Gray] = "#6B7280",
        [ProductColor.Red] = "#DC2626", [ProductColor.Orange] = "#EA580C", [ProductColor.Yellow] = "#CA8A04",
        [ProductColor.Green] = "#16A34A", [ProductColor.Teal] = "#0D9488", [ProductColor.Blue] = "#2563EB",
        [ProductColor.Navy] = "#1E3A8A", [ProductColor.Purple] = "#7C3AED", [ProductColor.Pink] = "#DB2777",
        [ProductColor.Brown] = "#92400E", [ProductColor.Beige] = "#A8875A", [ProductColor.Multi] = "#9333EA",
    };

    /// <inheritdoc/>
    public byte[] Write(string title, IReadOnlyList<HomeStockTemplateRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        WriteCounts(workbook.Worksheets.Add("Home stock"), rows);
        WriteInstructions(workbook.Worksheets.Add("How to use"), title);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteCounts(IXLWorksheet sheet, IReadOnlyList<HomeStockTemplateRow> rows)
    {
        string[] headers = [.. MappedHeaders, .. ReferenceHeaders];
        for (var c = 0; c < headers.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = headers[c];
        }

        for (var r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            var line = r + 2;

            // Values are set as typed cells (text or number), never formulas, so a product title
            // starting with "=" can't run as one.
            // The change column (2) starts empty: blank means no change, so only the rows that moved need typing.
            sheet.Cell(line, 1).Value = row.Sku;
            sheet.Cell(line, 3).Value = row.HomeStock;
            sheet.Cell(line, 4).Value = row.Title ?? string.Empty;
            sheet.Cell(line, 5).Value = row.Family ?? string.Empty;
            sheet.Cell(line, 6).Value = row.Color?.ToString() ?? string.Empty;
            sheet.Cell(line, 7).Value = row.AtAmazon;
            sheet.Cell(line, 8).Value = row.Inbound;
            sheet.Cell(line, 9).Value = row.Sold30d;
            if (row.SendToAmazon is { } send)
            {
                sheet.Cell(line, 10).Value = send;
            }

            if (row.Color is { } color && ColorHex.TryGetValue(color, out var hex))
            {
                sheet.Cell(line, 1).Style.Font.FontColor = XLColor.FromHtml(hex);
                sheet.Cell(line, 6).Style.Font.FontColor = XLColor.FromHtml(hex);
            }
        }

        var lastRow = Math.Max(rows.Count + 1, 2);
        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // The two columns the upload reads stand out; change is the one to type in.
        sheet.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
        sheet.Column(1).Style.Font.Bold = true;
        var changes = sheet.Range(2, 2, lastRow, 2);
        changes.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3");
        changes.Style.Font.Bold = true;
        changes.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        changes.CreateDataValidation().WholeNumber.Between(-InventoryItemService.MaxHomeStock, InventoryItemService.MaxHomeStock);
        sheet.Cell(1, 2).CreateComment().AddText("Units added or removed: 20 adds, -5 removes. Leave blank for no change.");

        sheet.Range(2, 3, lastRow, headers.Length).Style.Font.FontColor = XLColor.FromHtml("#64748B");
        sheet.Range(1, 7, lastRow, headers.Length).Style.NumberFormat.Format = "#,##0";
        sheet.Range(2, 3, lastRow, 3).Style.NumberFormat.Format = "#,##0";
        changes.Style.NumberFormat.Format = "+0;-0;0";

        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, lastRow, headers.Length).SetAutoFilter();
        sheet.Columns(1, headers.Length).AdjustToContents(1, Math.Min(lastRow, 500));
        sheet.Column(4).Width = Math.Min(sheet.Column(4).Width, 60);
        sheet.Column(2).Width = Math.Max(sheet.Column(2).Width, 12);
    }

    private static void WriteInstructions(IXLWorksheet sheet, string title)
    {
        string[] lines =
        [
            title,
            string.Empty,
            "1. On the \"Home stock\" sheet, type each change in the yellow change column: 20 adds 20 units, -5 removes 5.",
            "2. Leave the change blank for SKUs that didn't move. Leave the sku column and the header row as they are.",
            "3. Save as .xlsx and upload it on Inventory → Edit home stock → Upload a spreadsheet.",
            "4. Review what each SKU will have, choose how increases and decreases are logged, and apply.",
            string.Empty,
            "Only the sku and change columns are read. The grey columns, current home stock first, are for reference and are ignored.",
            "Blank or 0 leaves a SKU alone, and so do rows you delete. If a SKU appears twice, its changes add up.",
            "A change that would leave less than 0 at home is skipped and listed on the review.",
            "Every change is logged in the home-stock ledger as the type you choose on the review.",
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            sheet.Cell(i + 1, 1).Value = lines[i];
        }

        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Column(1).Width = 100;
    }
}
