using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>Live <see cref="IAmazonAwdGateway"/> over <see cref="AwdApiClient"/>.</summary>
/// <param name="client">Typed AWD API client.</param>
internal sealed class SpApiAwdGateway(AwdApiClient client) : IAmazonAwdGateway
{
    /// <inheritdoc/>
    public Task<AwdInventoryPage> ListInventoryPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken) =>
        client.ListInventoryAsync(marketplace, nextToken, cancellationToken);
}
