using AERai.Web.Domain.Staging;

namespace AERai.Web.Application.Imports;

/// <summary>
/// Outcome of promoting a staging batch into the curated tables.
/// </summary>
/// <param name="BatchId">The promoted batch.</param>
/// <param name="Status">The batch's status after promotion.</param>
/// <param name="PromotedRowCount">Rows written to <c>core</c>.</param>
/// <param name="RejectedRowCount">Rows rejected with an error message.</param>
public sealed record PromotionSummary(long BatchId, ImportBatchStatus Status, int PromotedRowCount, int RejectedRowCount);
