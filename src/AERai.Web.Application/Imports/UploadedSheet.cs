using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Reads a user-uploaded sheet (.xlsx, or .csv/.tsv/.txt text) into header-keyed records, so every
/// spreadsheet upload accepts the same formats with the same messages.
/// </summary>
public static class UploadedSheet
{
    /// <summary>Reads the file, choosing the parser by its extension.</summary>
    /// <param name="spreadsheets">Workbook reader for .xlsx files.</param>
    /// <param name="fileName">The uploaded file's name (only its extension is used).</param>
    /// <param name="content">The file; read, not disposed.</param>
    /// <param name="maxRows">Maximum data rows accepted.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The parsed sheet, or a failure for an unsupported, unreadable, or too-large file.</returns>
    public static async Task<Result<ParsedFile>> ReadAsync(ISpreadsheetReader spreadsheets, string fileName, Stream content, int maxRows, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(spreadsheets);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);

        switch (Path.GetExtension(fileName).ToLowerInvariant())
        {
            case ".xlsx":
                return await spreadsheets.ReadAsync(content, maxRows, cancellationToken).ConfigureAwait(false);
            case ".csv" or ".tsv" or ".txt":
                using (var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true))
                {
                    return await DelimitedTextParser.ParseAsync(reader, maxRows, cancellationToken).ConfigureAwait(false);
                }

            default:
                return Result.Failure<ParsedFile>("Upload a .xlsx, .csv, or .tsv file.");
        }
    }
}
