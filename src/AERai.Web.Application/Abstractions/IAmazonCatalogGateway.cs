using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Amazon's product catalog, used to find each listing's main picture. The live implementation calls
/// the SP-API Catalog Items API; the simulated one makes up pictures for local development.
/// </summary>
public interface IAmazonCatalogGateway
{
    /// <summary>Looks up the main listing picture of several ASINs in one marketplace.</summary>
    /// <param name="marketplace">Marketplace whose catalog to read; its region selects endpoint and credentials.</param>
    /// <param name="asins">ASINs to look up (batched by the implementation).</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The picture address per ASIN (ordinal, upper case); ASINs Amazon has no picture for are left out.</returns>
    Task<IReadOnlyDictionary<string, Uri>> FindMainImagesAsync(Marketplace marketplace, IReadOnlyCollection<string> asins, CancellationToken cancellationToken);

    /// <summary>Downloads a picture found by <see cref="FindMainImagesAsync"/>.</summary>
    /// <param name="url">The picture address.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The picture bytes as a stream the caller disposes.</returns>
    Task<Stream> OpenImageAsync(Uri url, CancellationToken cancellationToken);
}
