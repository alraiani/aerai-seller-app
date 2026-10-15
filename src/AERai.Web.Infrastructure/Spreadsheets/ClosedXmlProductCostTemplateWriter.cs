using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Products;
using ClosedXML.Excel;

namespace AERai.Web.Infrastructure.Spreadsheets;

/// <summary>
/// ClosedXML implementation of <see cref="IProductCostTemplateWriter"/>. The first sheet is exactly
/// what the cost upload reads (header row with <c>sku</c> and <c>cost-of-goods</c>); the other
/// columns are reference only and ignored on upload. A second sheet explains how to fill it in.
/// </summary>
internal sealed class ClosedXmlProductCostTemplateWriter : IProductCostTemplateWriter
{
    /// <summary>The two columns the upload reads; their header text must stay exactly this.</summary>
    internal static readonly string[] MappedHeaders = ["sku", "cost-of-goods"];

    /// <summary>Reference columns, ignored on upload.</summary>
    private static readonly string[] ReferenceHeaders = ["ASIN", "Product name", "Family", "Cost last set"];

    /// <inheritdoc/>
    public byte[] Write(string title, string currency, IReadOnlyList<ProductCostTemplateRow> rows)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(currency);
        ArgumentNullException.ThrowIfNull(rows);

        using var workbook = new XLWorkbook();
        WriteCosts(workbook.Worksheets.Add("Cost of goods"), currency, rows);
        WriteInstructions(workbook.Worksheets.Add("How to use"), title, currency);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteCosts(IXLWorksheet sheet, string currency, IReadOnlyList<ProductCostTemplateRow> rows)
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

            // Values are set as typed cells (text, number, or date), never formulas, so a product
            // title starting with "=" can't run as one.
            sheet.Cell(line, 1).Value = row.Sku;
            if (row.CostOfGoods is { } cost)
            {
                sheet.Cell(line, 2).Value = cost;
            }

            sheet.Cell(line, 3).Value = row.Asin ?? string.Empty;
            sheet.Cell(line, 4).Value = row.Title ?? string.Empty;
            sheet.Cell(line, 5).Value = row.Family ?? string.Empty;
            if (row.CostUpdatedAt is { } updated)
            {
                sheet.Cell(line, 6).Value = updated.UtcDateTime.Date;
            }
        }

        var lastRow = Math.Max(rows.Count + 1, 2);
        var header = sheet.Range(1, 1, 1, headers.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F1F5F9");
        header.Style.Border.BottomBorder = XLBorderStyleValues.Thin;

        // The two columns the upload reads stand out; the cost is the one to type in.
        sheet.Range(1, 1, 1, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
        sheet.Column(1).Style.Font.Bold = true;
        var costs = sheet.Range(2, 2, lastRow, 2);
        costs.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEF9C3");
        costs.Style.Font.Bold = true;
        costs.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        costs.Style.NumberFormat.Format = "#,##0.00";
        costs.CreateDataValidation().Decimal.Between(0, (double)ProductService.MaxCostOfGoods);
        sheet.Cell(1, 2).CreateComment().AddText($"Unit cost in {currency}. Leave blank to keep the current cost.");

        sheet.Range(2, 3, lastRow, headers.Length).Style.Font.FontColor = XLColor.FromHtml("#64748B");
        sheet.Range(2, 6, lastRow, 6).Style.NumberFormat.Format = "yyyy-mm-dd";

        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, lastRow, headers.Length).SetAutoFilter();
        sheet.Columns(1, headers.Length).AdjustToContents(1, Math.Min(lastRow, 500));
        sheet.Column(4).Width = Math.Min(sheet.Column(4).Width, 60);
        sheet.Column(2).Width = Math.Max(sheet.Column(2).Width, 14);
    }

    private static void WriteInstructions(IXLWorksheet sheet, string title, string currency)
    {
        string[] lines =
        [
            title,
            string.Empty,
            $"1. On the \"Cost of goods\" sheet, type each SKU's landed cost per unit in {currency} in the yellow cost-of-goods column.",
            "2. Leave the sku column and the first sheet's header row as they are.",
            "3. Save as .xlsx (or .csv) and upload it on Products & COGS → Upload. You'll review the changes before they're saved.",
            string.Empty,
            "Only the sku and cost-of-goods columns are read. The grey columns are for reference and are ignored.",
            "A blank cost leaves that SKU's cost as it is; to clear a cost, do it on the Products page.",
            "Rows you delete are left unchanged. If a SKU appears twice, the last row wins. Costs are rounded to cents.",
        ];
        for (var i = 0; i < lines.Length; i++)
        {
            sheet.Cell(i + 1, 1).Value = lines[i];
        }

        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;
        sheet.Column(1).Width = 110;
    }
}
