using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;

namespace AERai.Web.UI.Pages.Tools.Batches;

/// <summary>
/// Lists staging batches, newest first.
/// </summary>
/// <param name="batches">Batch queries.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IImportBatchQueries batches, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>The current page of batches.</summary>
    public PagedResult<ImportBatchSummary> Batches { get; private set; } = default!;

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Batches = await batches.ListAsync(marketplace.MarketplaceId, ToPageRequest(), cancellationToken);
    }
}
