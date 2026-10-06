using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using ClosedXML.Excel;

namespace AERai.Web.Infrastructure.Spreadsheets;

/// <summary>
/// <see cref="ISpreadsheetReader"/> using ClosedXML. Numbers are read as their invariant-culture
/// value rather than as displayed, so a quantity formatted as "1,200.00" still reads as 1200.
/// </summary>
internal sealed class ClosedXmlSpreadsheetReader : ISpreadsheetReader
{
    /// <inheritdoc/>
    public async Task<Result<ParsedFile>> ReadAsync(Stream content, int maxRows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        // ClosedXML needs a seekable stream; uploads are size-limited by the page, so buffering is bounded.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(buffer);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or FormatException or NotSupportedException or IOException)
        {
            return Result.Failure<ParsedFile>("The file is not a readable Excel workbook (.xlsx).");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault();
            var rows = sheet?.RowsUsed().ToList() ?? [];
            if (rows.Count == 0)
            {
                return Result.Failure<ParsedFile>("The workbook's first sheet is empty.");
            }

            var lastColumn = rows.Max(r => r.LastCellUsed()?.Address.ColumnNumber ?? 0);
            var headers = Cells(rows[0], lastColumn).Select(DelimitedTextParser.NormalizeHeader).ToList();
            if (rows.Count - 1 > maxRows)
            {
                return Result.Failure<ParsedFile>($"The sheet has more than {maxRows:N0} data rows. Split it into smaller files.");
            }

            var records = new List<ParsedRecord>(rows.Count - 1);
            foreach (var row in rows.Skip(1))
            {
                var values = Cells(row, lastColumn);
                var fields = new Dictionary<string, string>(headers.Count, StringComparer.Ordinal);
                for (var i = 0; i < headers.Count; i++)
                {
                    if (headers[i].Length > 0)
                    {
                        fields[headers[i]] = values[i];
                    }
                }

                // Row numbers count data rows from 1, like the delimited parser, so messages line up.
                records.Add(new ParsedRecord(row.RowNumber() - rows[0].RowNumber(), string.Join('\t', values), fields));
            }

            return Result.Success(new ParsedFile(headers, records));
        }
    }

    private static List<string> Cells(IXLRow row, int lastColumn) =>
        Enumerable.Range(1, lastColumn).Select(c => CellText(row.Cell(c))).ToList();

    private static string CellText(IXLCell cell) =>
        cell.Value.IsNumber
            ? cell.Value.GetNumber().ToString(CultureInfo.InvariantCulture)
            : cell.Value.ToString(CultureInfo.InvariantCulture).Trim();
}
