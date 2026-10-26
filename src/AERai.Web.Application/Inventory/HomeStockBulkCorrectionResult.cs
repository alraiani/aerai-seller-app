namespace AERai.Web.Application.Inventory;

/// <summary>What a bulk reason change did.</summary>
/// <param name="Updated">Entries changed (each recorded as a correction).</param>
/// <param name="Unchanged">Entries that already had the reason set.</param>
/// <param name="Skipped">Entries that couldn't change, each with why ("#12 SKU: …").</param>
public sealed record HomeStockBulkCorrectionResult(int Updated, int Unchanged, IReadOnlyList<string> Skipped);
