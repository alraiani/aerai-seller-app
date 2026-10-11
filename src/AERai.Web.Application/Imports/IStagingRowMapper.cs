using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Maps parsed records of one report type onto that type's staging entity (Strategy pattern —
/// one implementation per <see cref="ImportSource"/>).
/// </summary>
public interface IStagingRowMapper
{
    /// <summary>The report type this mapper handles.</summary>
    ImportSource Source { get; }

    /// <summary>Normalized column names that must be present in the header row.</summary>
    IReadOnlyList<string> RequiredColumns { get; }

    /// <summary>
    /// Appends the record to the batch's staging collection for this source. Values are copied as
    /// text; no type conversion or validation happens here (that is promotion's job).
    /// </summary>
    /// <param name="batch">The batch being built.</param>
    /// <param name="record">The parsed record.</param>
    void Append(ImportBatch batch, ParsedRecord record);
}
