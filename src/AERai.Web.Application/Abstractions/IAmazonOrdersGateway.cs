using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Orders from the SP-API Orders API (<c>searchOrders</c>, v2026-01-01). The live implementation goes
/// through the SP-API pipeline; the simulated one makes up orders for local development.
/// </summary>
public interface IAmazonOrdersGateway
{
    /// <summary>Gets one page of the marketplace's orders that match <paramref name="filter"/>.</summary>
    /// <param name="marketplace">Marketplace being synced; its region selects the endpoint and credentials.</param>
    /// <param name="filter">Which orders to return; sent unchanged with every page.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, with the token for the next one.</returns>
    Task<AmazonOrdersPage> SearchOrdersPageAsync(Marketplace marketplace, AmazonOrdersFilter filter, string? nextToken, CancellationToken cancellationToken);
}
