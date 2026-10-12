namespace AERai.Web.Application.Imports;

/// <summary>
/// A staging row that promotion rejected, shown so the user can correct the source file.
/// </summary>
/// <param name="RowNumber">1-based data row number in the file.</param>
/// <param name="ErrorMessage">Why it was rejected.</param>
/// <param name="RawLine">The original line of text.</param>
public sealed record RejectedRow(int RowNumber, string ErrorMessage, string RawLine);
