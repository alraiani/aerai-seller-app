using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Bookkeeping;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Domain.Reporting;
using AERai.Web.UI.Models;
using Microsoft.AspNetCore.Mvc;

namespace AERai.Web.UI.Pages.Bookkeeping;

/// <summary>
/// Settlements ready to export to QuickBooks, and the IIF files already generated for the current marketplace.
/// </summary>
/// <param name="reporting">Reporting queries (settlement totals).</param>
/// <param name="bookkeeping">Bookkeeping use cases.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IReportingQueries reporting, IBookkeepingService bookkeeping, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>The current page of settlements.</summary>
    public PagedResult<SettlementSummary> Settlements { get; private set; } = default!;

    /// <summary>Files already generated, newest first.</summary>
    public IReadOnlyList<BookkeepingExportSummary> Exports { get; private set; } = [];

    /// <summary>Settlements that have at least one generated file.</summary>
    public IReadOnlySet<string> ExportedSettlementIds { get; private set; } = new HashSet<string>();

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Settlements = await reporting.GetSettlementsAsync(marketplace.MarketplaceId, ToPageRequest(), null, cancellationToken);
        Exports = await bookkeeping.GetExportsAsync(marketplace.MarketplaceId, cancellationToken);
        ExportedSettlementIds = Exports.Select(e => e.SettlementId).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Downloads a generated IIF file.</summary>
    /// <param name="id">Export identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The file, or 404 when it does not exist in the current marketplace.</returns>
    public async Task<IActionResult> OnGetDownloadAsync(long id, CancellationToken cancellationToken)
    {
        var marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        var file = await bookkeeping.GetExportFileAsync(marketplace.MarketplaceId, id, cancellationToken);
        if (file is null)
        {
            return NotFound();
        }

        // IIF is plain text for QuickBooks Desktop; no byte-order mark, which it would read as part of the first header.
        return File(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(file.Content), "application/octet-stream", file.FileName);
    }
}
