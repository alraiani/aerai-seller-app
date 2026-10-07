namespace AERai.Web.Application.Imports;

/// <summary>
/// The header and data records of a delimited file.
/// </summary>
/// <param name="Headers">Normalized column names in file order.</param>
/// <param name="Records">Data records in file order (blank lines omitted).</param>
public sealed record ParsedFile(IReadOnlyList<string> Headers, IReadOnlyList<ParsedRecord> Records);
