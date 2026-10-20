using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>Live <see cref="IAmazonFinancesGateway"/> over <see cref="FinancesApiClient"/>.</summary>
/// <param name="client">Typed Finances API client.</param>
internal sealed class SpApiFinancesGateway(FinancesApiClient client) : IAmazonFinancesGateway
{
    /// <inheritdoc/>
    public Task<DeferredTransactionPage> ListTransactionsPageAsync(
        Marketplace marketplace, DeferredTransactionListing listing, DateTimeOffset postedAfter, string? nextToken, CancellationToken cancellationToken) =>
        client.ListDeferredAsync(marketplace, listing, postedAfter, nextToken, cancellationToken);
}
