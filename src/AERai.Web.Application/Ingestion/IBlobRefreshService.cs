using AERai.Web.Application.Common;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Loads the database from files already in raw storage, without calling Amazon. Upload report files or
/// Orders API pages to the raw container (any tool, e.g. Storage Explorer), then refresh.
/// </summary>
/// <remarks>
/// Supported folders: <c>orders/</c>, <c>settlements/</c>, <c>inventory/</c> (report files, staged then
/// optionally promoted) and <c>ordersapi/</c> (Orders API pages, written straight to the orders). The
/// snapshot folders (<c>fbainventory/</c>, <c>fbareservedinventory/</c>, <c>fbarestockrecommendations/</c>,
/// <c>awdinventory/</c>) are never loaded: those reports are dated by the day they arrive, so loading an old
/// file would date it today.
/// </remarks>
public interface IBlobRefreshService
{
    /// <summary>Counts what a refresh would pick up.</summary>
    /// <param name="request">The same settings the refresh would use.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The counts.</returns>
    Task<BlobRefreshPreview> PreviewAsync(BlobRefreshRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Loads every file not loaded yet (or all files, when asked to reprocess), oldest first, at most
    /// <see cref="BlobRefreshService.MaxFilesPerRefresh"/> at a time. One bad file is reported and does not stop the rest.
    /// </summary>
    /// <param name="request">How to treat the files.</param>
    /// <param name="user">Email of the user refreshing.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was loaded, or a failure when the default marketplace does not exist or is not active.</returns>
    Task<Result<BlobRefreshSummary>> RefreshAsync(BlobRefreshRequest request, string user, CancellationToken cancellationToken);
}
