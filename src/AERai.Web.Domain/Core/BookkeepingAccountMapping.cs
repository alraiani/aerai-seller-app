namespace AERai.Web.Domain.Core;

/// <summary>
/// Maps one kind of settlement amount to the QuickBooks account it posts to in the IIF export.
/// </summary>
/// <remarks>
/// The key is Amazon's own <c>amount-type</c>/<c>amount-description</c> pair (e.g. <c>ItemFees</c>/<c>Commission</c>),
/// so the mapping follows exactly what Amazon reports instead of a hard-coded classification. Several
/// pairs may share one account (all fee kinds into "Amazon Fees"); the export sums them. Mappings are
/// shared across marketplaces because the QuickBooks chart of accounts belongs to the company, not to a
/// marketplace. A pair with no row is unmapped, and a settlement containing one cannot be exported.
/// </remarks>
public sealed class BookkeepingAccountMapping
{
    /// <summary>Surrogate key.</summary>
    public int Id { get; set; }

    /// <summary>
    /// Settlement amount type. Amazon leaves it blank on some rows (e.g. other transactions); those use the
    /// row's transaction type instead.
    /// </summary>
    public required string AmountType { get; set; }

    /// <summary>Settlement amount description; empty when Amazon sends none.</summary>
    public required string AmountDescription { get; set; }

    /// <summary>Exact name of the QuickBooks account, as it appears in the company file.</summary>
    public required string QuickBooksAccountName { get; set; }

    /// <summary>When the mapping last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Email of the user who last changed it.</summary>
    public required string UpdatedBy { get; set; }
}
