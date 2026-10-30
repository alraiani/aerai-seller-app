using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Marketplaces;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

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

    /// <summary>The marketplace's time zone (IANA id), for upload times.</summary>
    public string TimeZoneId { get; private set; } = "UTC";

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, or only its list for a partial reload.</returns>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        TimeZoneId = marketplace.TimeZoneId;
        Batches = await batches.ListAsync(marketplace.MarketplaceId, ToPageRequest(), cancellationToken);
        return this.PageOrFragment("_BatchesBody");
    }
}
