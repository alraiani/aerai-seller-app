using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Orders gateway registered when <see cref="SpApiMode.Disabled"/>: every call fails with a clear
/// message, which the run records in its history.
/// </summary>
internal sealed class UnavailableOrdersGateway : IAmazonOrdersGateway
{
    /// <inheritdoc/>
    public Task<AmazonOrdersPage> SearchOrdersPageAsync(Marketplace marketplace, AmazonOrdersFilter filter, string? nextToken, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Amazon SP-API is disabled. Set SpApi:Mode to Live (with credentials) or Simulated.");
}
