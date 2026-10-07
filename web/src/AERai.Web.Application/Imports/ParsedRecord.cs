namespace AERai.Web.Application.Imports;

/// <summary>
/// One data record read from a delimited file, keyed by normalized column name.
/// </summary>
/// <param name="RowNumber">1-based data row number (the header is row 0).</param>
/// <param name="RawLine">The record's original text, including embedded newlines inside quotes.</param>
/// <param name="Fields">Values keyed by normalized header (see <see cref="DelimitedTextParser.NormalizeHeader"/>).</param>
public sealed record ParsedRecord(int RowNumber, string RawLine, IReadOnlyDictionary<string, string> Fields)
{
    /// <summary>
    /// Gets a trimmed field value, or <see langword="null"/> when the column is missing or blank.
    /// </summary>
    /// <param name="column">Normalized column name, e.g. <c>amazon-order-id</c>.</param>
    /// <param name="maxLength">Values longer than this are truncated so they fit the staging column.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    public string? Get(string column, int maxLength = Domain.Staging.StagingRow.MaxFieldLength)
    {
        if (!Fields.TryGetValue(column, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
