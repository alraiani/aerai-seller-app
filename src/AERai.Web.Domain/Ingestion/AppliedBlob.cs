namespace AERai.Web.Domain.Ingestion;

/// <summary>
/// A raw file the blob refresh has already loaded into the database, so refreshing again skips it.
/// </summary>
/// <remarks>
/// Report files that were staged are also known from their import batch's raw path; this ledger exists
/// for the files that leave no batch: Orders API pages, and report files that turned out to be empty.
/// Rows are never edited, only added.
/// </remarks>
public sealed class AppliedBlob
{
    /// <summary>Path of the file inside the raw container (natural key).</summary>
    public required string Path { get; set; }

    /// <summary>The raw folder the file was in (<c>ordersapi</c>, <c>settlements</c>, …).</summary>
    public required string Source { get; set; }

    /// <summary>What loading it did, e.g. <c>12 orders</c> or <c>empty file</c>.</summary>
    public string? Outcome { get; set; }

    /// <summary>When it was loaded.</summary>
    public DateTimeOffset AppliedAt { get; set; }

    /// <summary>Email of the user who ran the refresh.</summary>
    public required string AppliedBy { get; set; }
}
