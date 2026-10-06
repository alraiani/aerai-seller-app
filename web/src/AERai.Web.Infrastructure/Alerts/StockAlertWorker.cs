using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Alerts;
using AERai.Web.Application.Inventory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.Alerts;

/// <summary>
/// Background service that re-evaluates stock alerts for every active marketplace on a fixed
/// interval (<see cref="InventoryOptions.AlertRefreshMinutes"/>), so new inventory and sales data
/// (from scheduled syncs or manual imports) turn into alerts without anyone opening a page.
/// </summary>
/// <remarks>
/// Unlike <see cref="Ingestion.SyncSchedulerWorker"/>, there is no claim between instances: every
/// instance refreshes every marketplace each interval. That is deliberate — a refresh is a few reads
/// and a compare, writes only differences, and is idempotent, and the database allows only one open
/// alert per SKU — so concurrent refreshes converge on the same state and a claim would add a table
/// and a failure mode for no real saving.
/// </remarks>
/// <param name="scopes">Creates a DI scope per refresh (services are scoped to the DbContext).</param>
/// <param name="options">Refresh interval.</param>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
internal sealed partial class StockAlertWorker(
    IServiceScopeFactory scopes,
    IOptions<InventoryOptions> options,
    TimeProvider clock,
    ILogger<StockAlertWorker> logger) : BackgroundService
{
    /// <summary>Pause after startup so migrations and seeding finish before the first refresh.</summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.AlertRefreshMinutes);
        try
        {
            await Task.Delay(StartupDelay, clock, stoppingToken).ConfigureAwait(false);
            using var timer = new PeriodicTimer(interval, clock);
            do
            {
                await RefreshAllAsync(stoppingToken).ConfigureAwait(false);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
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
