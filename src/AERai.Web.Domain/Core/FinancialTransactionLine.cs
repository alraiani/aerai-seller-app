namespace AERai.Web.Domain.Core;

/// <summary>
/// One amount of a <see cref="FinancialTransaction"/>: a leaf of Amazon's (nested) breakdowns, for one item's
/// product when the transaction has items. A transaction's lines add up to its total; any part Amazon does
/// not break down is kept as an <c>(unitemized)</c> line so nothing is lost.
/// </summary>
public sealed class FinancialTransactionLine
{
    /// <summary>Surrogate key.</summary>
    public long Id { get; set; }

    /// <summary>The transaction the line belongs to.</summary>
    public long FinancialTransactionId { get; set; }

    /// <summary>The item's seller SKU, or <see langword="null"/> for an amount that is not for a product.</summary>
    public string? Sku { get; set; }

    /// <summary>The item's ASIN, when Amazon gives one.</summary>
    public string? Asin { get; set; }

    /// <summary>
    /// The item's shipped quantity, on the first line of each item only (0 on the rest), so adding a column of
    /// lines counts each item once.
    /// </summary>
    public int Units { get; set; }

    /// <summary>Amazon's breakdown types from the top level down, joined with <c>/</c>, e.g. <c>AmazonFees/Commission</c>.</summary>
    public required string BreakdownPath { get; set; }

    /// <summary>The amount, signed like the transaction.</summary>
    public decimal Amount { get; set; }
}
