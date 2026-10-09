using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Alerts;
using AERai.Web.Application.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Alerts;

/// <summary>
/// Background service that re-evaluates stock alerts for every active marketplace when their inputs
/// change (an <see cref="IStockAlertRefreshSignal"/> request after promotion or a home-stock /
/// lead-time edit), so new data turns into alerts without anyone opening a page. A slow safety-net
/// interval (<see cref="InventoryOptions.AlertRefreshMinutes"/>, daily by default) also catches
/// statuses that change only because time passes (days of cover running down).
/// </summary>
/// <remarks>
/// Unlike <see cref="Ingestion.SyncSchedulerWorker"/>, there is no claim between instances: every
/// instance refreshes every marketplace each interval. That is deliberate — a refresh is a few reads
/// and a compare, writes only differences, and is idempotent, and the database allows only one open
/// alert per SKU — so concurrent refreshes converge on the same state and a claim would add a table
/// and a failure mode for no real saving.
/// </remarks>
/// <param name="scopes">Creates a DI scope per refresh (services are scoped to the DbContext).</param>
/// <param name="signal">Refresh requests.</param>
/// <param name="options">Refresh interval.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class StockAlertWorker(
    IServiceScopeFactory scopes,
    StockAlertRefreshSignal signal,
    IOptions<InventoryOptions> options,
    TimeProvider clock,
    ILogger<StockAlertWorker> logger) : BackgroundService
{
    /// <summary>Pause after startup so migrations and seeding finish before the first refresh.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Wait after a request before refreshing, so a burst of changes (a run promoting several batches,
    /// a bulk home-stock edit) becomes one refresh.
    /// </summary>
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(10);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.AlertRefreshMinutes);
        try
        {
            await Task.Delay(StartupDelay, clock, stoppingToken).ConfigureAwait(false);
            while (true)
            {
                // Clear requests made before this refresh starts: it already sees their changes.
                signal.Clear();
                await RefreshAllAsync(stoppingToken).ConfigureAwait(false);
                if (await signal.WaitAsync(interval, stoppingToken).ConfigureAwait(false))
                {
                    await Task.Delay(Debounce, clock, stoppingToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task RefreshAllAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var marketplaces = await scope.ServiceProvider.GetRequiredService<IMarketplaceQueries>().GetAllAsync(cancellationToken).ConfigureAwait(false);
            var alerts = scope.ServiceProvider.GetRequiredService<IStockAlertService>();
            foreach (var marketplace in marketplaces.Where(m => m.IsActive))
            {
                await alerts.RefreshAsync(marketplace, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The worker must survive transient failures (e.g. the database being briefly unavailable).
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogRefreshFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Stock alert refresh failed; retrying at the next interval")]
    private partial void LogRefreshFailed(Exception exception);
}
