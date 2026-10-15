namespace AERai.Web.Application.Ingestion;

/// <summary>What queueing "Run now" did.</summary>
/// <param name="Wait">Set when the run's report request will wait for Amazon's request limit; otherwise <see langword="null"/>.</param>
public sealed record RunNowResult(RequestWait? Wait);
