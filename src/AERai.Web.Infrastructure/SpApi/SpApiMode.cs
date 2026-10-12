namespace AERai.Web.Infrastructure.SpApi;

/// <summary>How the app connects to Amazon.</summary>
public enum SpApiMode
{
    /// <summary>No connection; scheduled pulls and "Run now" are unavailable.</summary>
    Disabled = 0,

    /// <summary>
    /// Built-in simulator that generates realistic report files locally, so the whole pipeline
    /// (schedules → blob → staging → promotion) can be exercised without Amazon credentials.
    /// Development only.
    /// </summary>
    Simulated = 1,

    /// <summary>Real SP-API calls with the configured LWA credentials.</summary>
    Live = 2,
}
