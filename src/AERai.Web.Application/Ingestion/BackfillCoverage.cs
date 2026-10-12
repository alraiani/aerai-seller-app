namespace AERai.Web.Application.Ingestion;

/// <summary>An earlier backfill that already pulled a range, found before queueing a new one.</summary>
/// <param name="Window">The range the earlier backfill covered.</param>
/// <param name="CompletedAt">When it finished.</param>
/// <param name="TriggeredBy">Who ran it.</param>
public sealed record BackfillCoverage(BackfillWindow Window, DateTimeOffset CompletedAt, string TriggeredBy);
