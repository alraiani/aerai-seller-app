using System.Diagnostics;
using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// <see cref="IAmazonConnectionTester"/> for every <see cref="SpApiMode"/>. In Live mode it makes one
/// <c>getReports</c> call through the normal pipeline (token exchange, rate limiter, retries), so a
/// pass means scheduled pulls can authenticate. Simulated and Disabled modes answer without calling
/// Amazon.
/// </summary>
/// <remarks>
/// Registered as a singleton so the cached result is shared by all users: <c>getReports</c> has a
/// small burst quota shared with ingestion, and repeated clicks must not eat into it.
/// </remarks>
/// <param name="services">Resolves the typed <see cref="ReportsApiClient"/> (Live mode only).</param>
/// <param name="connection">Configured mode and missing-credential problems.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class AmazonConnectionTester(
    IServiceProvider services,
    IAmazonConnectionInfo connection,
    TimeProvider clock,
    ILogger<AmazonConnectionTester> logger) : IAmazonConnectionTester, IDisposable
{
    /// <summary>How long a result is reused before Amazon is called again.</summary>
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <inheritdoc/>
    public ConnectionTestResult? LastResult { get; private set; }

    /// <inheritdoc/>
    public async Task<ConnectionTestResult> TestAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (LastResult is { } cached && clock.GetUtcNow() - cached.TestedAt < CacheFor)
            {
                return cached;
            }

            LastResult = await RunTestAsync(cancellationToken).ConfigureAwait(false);
            return LastResult;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public void Dispose() => _gate.Dispose();

    private async Task<ConnectionTestResult> RunTestAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        if (connection.Problem is { } problem)
        {
            return new ConnectionTestResult(false, problem, now);
        }

        if (connection.Mode != nameof(SpApiMode.Live))
        {
            return new ConnectionTestResult(true, "Simulated mode — sample reports are generated locally; Amazon is not called.", now);
        }

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = services.GetRequiredService<ReportsApiClient>();
            await client.PingAsync(AmazonRegion.NorthAmerica, SpApiReportTypes.ToCode(AmazonReportType.Orders), cancellationToken).ConfigureAwait(false);
            return new ConnectionTestResult(true, $"Connected — Amazon responded in {stopwatch.ElapsedMilliseconds} ms.", clock.GetUtcNow());
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Any failure is the answer to "does the connection work?", so it is reported, not thrown.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Pipeline and LWA exception messages never include credentials or tokens.
            LogTestFailed(ex);
            return new ConnectionTestResult(false, $"Amazon rejected the test call: {ex.Message}", clock.GetUtcNow());
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Amazon connection test failed")]
    private partial void LogTestFailed(Exception exception);
}
