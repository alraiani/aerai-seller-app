using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Deferred transactions from the SP-API Finances API (<c>listTransactions</c>, v2024-06-19). The live
/// implementation calls Amazon through the SP-API pipeline; the simulated one makes up deferrals for
/// local development.
/// </summary>
public interface IAmazonFinancesGateway
{
    /// <summary>Gets one page of the transactions Amazon still holds, or of those it deferred and has since released.</summary>
    /// <param name="marketplace">Marketplace being synced; its region selects the endpoint and credentials.</param>
    /// <param name="listing">Still held, or released.</param>
    /// <param name="postedAfter">Only transactions posted on or after this instant (Amazon allows a window of up to 180 days).</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, with the token for the next one.</returns>
    Task<DeferredTransactionPage> ListTransactionsPageAsync(
        Marketplace marketplace, DeferredTransactionListing listing, DateTimeOffset postedAfter, string? nextToken, CancellationToken cancellationToken);
}
