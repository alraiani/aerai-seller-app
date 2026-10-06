using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Abstractions;

/// <summary>Reads the first worksheet of an Excel workbook (.xlsx) into header-keyed records.</summary>
public interface ISpreadsheetReader
{
    /// <summary>
    /// Reads the first worksheet. The first non-empty row is the header (normalized the same way as
    /// <see cref="DelimitedTextParser.NormalizeHeader"/>); each later non-empty row is a record.
    /// </summary>
    /// <param name="content">The workbook; read, not disposed.</param>
    /// <param name="maxRows">Maximum data rows accepted; more fails the read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The parsed sheet, or a failure when the file is not a readable workbook or is too large.</returns>
    Task<Result<ParsedFile>> ReadAsync(Stream content, int maxRows, CancellationToken cancellationToken);
}
