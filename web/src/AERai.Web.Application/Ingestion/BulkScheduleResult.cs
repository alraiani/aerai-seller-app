namespace AERai.Web.Application.Ingestion;

/// <summary>Outcome of a bulk schedule action.</summary>
/// <param name="Succeeded">How many schedules the action was applied to.</param>
/// <param name="Errors">Why each remaining schedule was skipped.</param>
public sealed record BulkScheduleResult(int Succeeded, IReadOnlyList<string> Errors);
