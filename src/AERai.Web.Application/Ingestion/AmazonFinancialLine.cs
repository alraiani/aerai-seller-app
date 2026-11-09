namespace AERai.Web.Application.Ingestion;

/// <summary>One amount of a ledger transaction: a leaf breakdown, for an item's product when there is one.</summary>
/// <param name="Sku">The item's seller SKU, or <see langword="null"/>.</param>
/// <param name="Asin">The item's ASIN, or <see langword="null"/>.</param>
/// <param name="Units">The item's shipped quantity on its first line, 0 on the rest.</param>
/// <param name="BreakdownPath">Breakdown types from the top down, joined with <c>/</c>.</param>
/// <param name="Amount">The amount, signed.</param>
public sealed record AmazonFinancialLine(string? Sku, string? Asin, int Units, string BreakdownPath, decimal Amount)
{
    /// <summary>Path of the line that keeps whatever part of a total Amazon did not break down.</summary>
    public const string Unitemized = "(unitemized)";
}
