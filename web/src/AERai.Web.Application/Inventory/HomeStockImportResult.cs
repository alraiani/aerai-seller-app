using AERai.Web.Application.Imports;

namespace AERai.Web.Application.Inventory;

/// <summary>The outcome of a home-stock spreadsheet upload.</summary>
/// <param name="Saved">Rows saved.</param>
/// <param name="Rejected">Rows that could not be saved, with the reason.</param>
public sealed record HomeStockImportResult(int Saved, IReadOnlyList<RejectedRow> Rejected);
