namespace AERai.Web.Domain.Staging;

/// <summary>
/// Common shape of every raw staging row: where it came from and whether promotion rejected it.
/// </summary>
/// <remarks>
/// Staging rows intentionally store business values as unparsed strings so that malformed input
/// still lands and can be inspected; typing happens during promotion into <c>core</c>.
/// </remarks>
public abstract class StagingRow
{
    /// <summary>
    /// Maximum length of any raw business column. Values longer than this are truncated on import
    /// (the full text is still preserved in <see cref="RawLine"/>).
    /// </summary>
    public const int MaxFieldLength = 400;

    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>The batch this row was uploaded in.</summary>
    public long ImportBatchId { get; set; }

    /// <summary>1-based line number in the source file (excluding the header row).</summary>
    public int RowNumber { get; set; }

    /// <summary>The original, unmodified line of text, preserved for troubleshooting.</summary>
    public required string RawLine { get; set; }

    /// <summary>Why promotion rejected this row; <see langword="null"/> when the row was promoted or not yet processed.</summary>
    public string? ErrorMessage { get; set; }
}
