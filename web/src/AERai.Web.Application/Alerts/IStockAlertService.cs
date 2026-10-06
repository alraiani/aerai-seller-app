using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Alerts;

/// <summary>Raises and resolves low/out-of-stock alerts, and serves them to users.</summary>
public interface IStockAlertService
{
    /// <summary>Re-evaluates every SKU in a marketplace and opens, updates, or resolves alerts to match.</summary>
    /// <param name="marketplace">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The changes made (empty when nothing changed or another instance got there first).</returns>
    Task<StockAlertChanges> RefreshAsync(Marketplace marketplace, CancellationToken cancellationToken);

    /// <summary>Unread open alerts for the bell.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="userEmail">User.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The count.</returns>
    Task<int> CountUnreadAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken);

    /// <summary>Open alerts and those resolved in the last week, for the Notifications page.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="userEmail">User.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Open alerts first (unread, then most serious, then newest), then resolved ones.</returns>
    Task<IReadOnlyList<StockAlertView>> ListAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken);

    /// <summary>Marks one alert read for a user.</summary>
    /// <param name="marketplaceId">Marketplace the alert belongs to.</param>
    /// <param name="alertId">Alert.</param>
    /// <param name="userEmail">User.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when saved.</returns>
    Task MarkReadAsync(string marketplaceId, long alertId, string userEmail, CancellationToken cancellationToken);

    /// <summary>Marks every open alert in a marketplace read for a user.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="userEmail">User.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many were marked.</returns>
    Task<int> MarkAllReadAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken);
}
