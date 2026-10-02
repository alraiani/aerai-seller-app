namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Describes how the app is connected to Amazon, for display on the Schedules page.
/// Never exposes credential values.
/// </summary>
public interface IAmazonConnectionInfo
{
    /// <summary>"Live", "Simulated", or "Disabled".</summary>
    string Mode { get; }

    /// <summary>Whether scheduled pulls can run (live with credentials present, or simulated).</summary>
    bool CanRun { get; }

    /// <summary>What is missing when <see cref="CanRun"/> is <see langword="false"/>.</summary>
    string? Problem { get; }
}
