namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Asks the background stock-alert refresh to run soon, because data that alerts depend on changed:
/// promoted Amazon data (inventory, sales, restock advice), home stock, or lead times.
/// </summary>
/// <remarks>
/// Alerts are refreshed when their inputs change rather than on a short fixed timer, so the
/// serverless database is not woken every few minutes just to find nothing new. Several requests
/// close together are coalesced into one refresh.
/// </remarks>
public interface IStockAlertRefreshSignal
{
    /// <summary>Requests a refresh. Never blocks and never fails.</summary>
    void Request();
}
