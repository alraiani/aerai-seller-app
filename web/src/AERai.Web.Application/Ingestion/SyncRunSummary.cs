using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>Outcome of one ingestion run.</summary>
/// <param name="RunId">The run's id.</param>
/// <param name="Status">Final status.</param>
/// <param name="Message">Human-readable summary or error.</param>
/// <param name="ImportBatchIds">Staging batches created.</param>
public sealed record SyncRunSummary(long RunId, SyncRunStatus Status, string Message, IReadOnlyList<long> ImportBatchIds);
