namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Proves the Amazon connection works end to end (token exchange + an authorized API call), rather
/// than only that credentials are configured.
/// </summary>
public interface IAmazonConnectionTester
{
    /// <summary>The most recent test result, or <see langword="null"/> if none has run yet.</summary>
    ConnectionTestResult? LastResult { get; }

    /// <summary>
    /// Runs a test. Results are reused for a short time because the call shares an SP-API quota
    /// with scheduled ingestion.
    /// </summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The result. Failures are returned, not thrown.</returns>
    Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken);
}
