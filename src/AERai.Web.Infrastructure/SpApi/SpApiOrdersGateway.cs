using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>Live <see cref="IAmazonOrdersGateway"/> over <see cref="OrdersApiClient"/>.</summary>
/// <param name="client">Typed Orders API client.</param>
internal sealed class SpApiOrdersGateway(OrdersApiClient client) : IAmazonOrdersGateway
{
    /// <inheritdoc/>
    public Task<AmazonOrdersPage> SearchOrdersPageAsync(Marketplace marketplace, AmazonOrdersFilter filter, string? nextToken, CancellationToken cancellationToken) =>
        client.SearchOrdersAsync(marketplace, filter, nextToken, cancellationToken);
}
