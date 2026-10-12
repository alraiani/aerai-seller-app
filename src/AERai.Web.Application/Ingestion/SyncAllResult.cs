namespace AERai.Web.Application.Ingestion;

/// <summary>What queueing "sync all" did.</summary>
/// <param name="Queued">Names of the schedules that were queued to run now.</param>
/// <param name="Skipped">Names of enabled schedules left out because their previous run is still waiting on Amazon.</param>
public sealed record SyncAllResult(IReadOnlyList<string> Queued, IReadOnlyList<string> Skipped);
