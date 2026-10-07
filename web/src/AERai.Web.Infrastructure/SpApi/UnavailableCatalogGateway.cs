using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Catalog gateway registered when <see cref="SpApiMode.Disabled"/>: every call fails with a clear
/// message. The picture service checks <see cref="IAmazonConnectionInfo.CanRun"/> first, so this is a backstop.
/// </summary>
internal sealed class UnavailableCatalogGateway : IAmazonCatalogGateway
{
    private const string Message = "Amazon SP-API is disabled. Set SpApi:Mode to Live (with credentials) or Simulated.";

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, Uri>> FindMainImagesAsync(Marketplace marketplace, IReadOnlyCollection<string> asins, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);

    /// <inheritdoc/>
    public Task<Stream> OpenImageAsync(Uri url, CancellationToken cancellationToken) =>
        throw new InvalidOperationException(Message);
}
