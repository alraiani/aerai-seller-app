using AERai.Web.Application.Alerts;
using AERai.Web.Domain.Alerts;

namespace AERai.Web.Application.Abstractions;

/// <summary>Persistence for stock alerts and their per-user read state.</summary>
public interface IStockAlertRepository
{
    /// <summary>A marketplace's open alerts.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The open alerts.</returns>
    Task<IReadOnlyList<StockAlert>> GetOpenAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Saves one refresh's changes in a single transaction.</summary>
    /// <param name="changes">The changes.</param>
    /// <param name="now">Timestamp for resolutions and message updates.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// <see langword="false"/> when another instance changed the same alerts at the same time (nothing
    /// is saved; the next refresh converges).
    /// </returns>
    Task<bool> ApplyAsync(StockAlertChanges changes, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Open alerts in a marketplace the user has not read.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="userEmail">User.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The count.</returns>
    Task<int> CountUnreadAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken);

    /// <summary>Open alerts plus those resolved since a point in time, newest first, with the user's read state.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="userEmail">User.</param>
    /// <param name="resolvedSince">Oldest resolution to include.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The alerts.</returns>
    Task<IReadOnlyList<StockAlertView>> ListAsync(string marketplaceId, string userEmail, DateTimeOffset resolvedSince, CancellationToken cancellationToken);

    /// <summary>Marks alerts read for a user (already-read ones are left alone).</summary>
    /// <param name="marketplaceId">Marketplace the alerts must belong to.</param>
    /// <param name="alertIds">Alerts to mark, or <see langword="null"/> for every open alert in the marketplace.</param>
    /// <param name="userEmail">User.</param>
    /// <param name="now">Read timestamp.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many were newly marked.</returns>
    Task<int> MarkReadAsync(string marketplaceId, IReadOnlyCollection<long>? alertIds, string userEmail, DateTimeOffset now, CancellationToken cancellationToken);
}
