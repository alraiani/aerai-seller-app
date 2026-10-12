namespace AERai.Web.Domain.Staging;

/// <summary>
/// Lifecycle state of an <see cref="ImportBatch"/> as it moves from upload to the curated tables.
/// </summary>
public enum ImportBatchStatus
{
    /// <summary>Rows have landed in staging and have not been promoted yet.</summary>
    Received = 1,

    /// <summary>Every row was promoted into <c>core</c>.</summary>
    Promoted = 2,

    /// <summary>Promotion completed, but one or more rows were rejected (see each row's error message).</summary>
    PromotedWithErrors = 3,

    /// <summary>Promotion rolled back because of an unexpected database error.</summary>
    Failed = 4,
}
