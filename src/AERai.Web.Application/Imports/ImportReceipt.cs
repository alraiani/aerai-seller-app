namespace AERai.Web.Application.Imports;

/// <summary>
/// Confirmation that a file landed in staging.
/// </summary>
/// <param name="BatchId">The new batch's id.</param>
/// <param name="RowCount">Data rows staged.</param>
public sealed record ImportReceipt(long BatchId, int RowCount);
