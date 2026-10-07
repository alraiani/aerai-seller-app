using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Live <see cref="IAmazonCatalogGateway"/>: looks pictures up through <see cref="CatalogItemsApiClient"/>
/// and downloads them from Amazon's picture CDN.
/// </summary>
/// <param name="client">Typed Catalog Items API client.</param>
/// <param name="httpClientFactory">Factory for the picture-download client.</param>
internal sealed class SpApiCatalogGateway(CatalogItemsApiClient client, IHttpClientFactory httpClientFactory) : IAmazonCatalogGateway
{
    /// <summary>
    /// Named client for picture downloads. It deliberately bypasses the SP-API pipeline: pictures are
    /// public CDN files, and the SP-API access token must never be sent anywhere but SP-API.
    /// </summary>
    public const string ImageHttpClientName = "AmazonImages";

    /// <summary>The picture size aimed for: big enough for any thumbnail, small enough to stay far below 2 MB.</summary>
    public const int PreferredMaxPixels = 1000;

    // Hosts Amazon serves catalog pictures from; anything else in a response is not downloaded.
    private static readonly string[] ImageHostSuffixes = [".media-amazon.com", ".ssl-images-amazon.com", ".images-amazon.com"];

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, Uri>> FindMainImagesAsync(Marketplace marketplace, IReadOnlyCollection<string> asins, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(asins);

        var found = new Dictionary<string, Uri>(StringComparer.Ordinal);
        foreach (var chunk in asins.Select(a => a.Trim().ToUpperInvariant()).Distinct(StringComparer.Ordinal).Chunk(CatalogItemsApiClient.MaxIdentifiersPerCall))
        {
            foreach (var item in await client.SearchByAsinAsync(marketplace, chunk, cancellationToken).ConfigureAwait(false))
            {
                if (PickMainImage(item, marketplace.MarketplaceId) is { } link)
                {
                    found[item.Asin.ToUpperInvariant()] = link;
                }
            }
        }

        return found;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenImageAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!IsAmazonImageHost(url))
        {
            throw new InvalidOperationException($"Not an Amazon picture address: {url.Host}.");
        }

        var response = await httpClientFactory.CreateClient(ImageHttpClientName)
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        try
        {
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The item's MAIN picture in the marketplace (any marketplace when it has none there): the largest
    /// size within <see cref="PreferredMaxPixels"/>, or the smallest size when all are bigger.
    /// </summary>
    /// <param name="item">The catalog item.</param>
    /// <param name="marketplaceId">Marketplace to prefer.</param>
    /// <returns>The picture address, or <see langword="null"/> when the item has no main picture.</returns>
    internal static Uri? PickMainImage(CatalogItem item, string marketplaceId)
    {
        var sets = item.Images ?? [];
        var main = (sets.FirstOrDefault(s => s.MarketplaceId == marketplaceId)?.Images ?? [])
            .Where(i => string.Equals(i.Variant, "MAIN", StringComparison.OrdinalIgnoreCase) && IsAmazonImageHost(i.Link))
            .ToList();
        if (main.Count == 0)
        {
            main = sets.SelectMany(s => s.Images ?? [])
                .Where(i => string.Equals(i.Variant, "MAIN", StringComparison.OrdinalIgnoreCase) && IsAmazonImageHost(i.Link))
                .ToList();
        }

        var fitting = main.Where(i => Math.Max(i.Width, i.Height) <= PreferredMaxPixels).MaxBy(i => Math.Max(i.Width, i.Height));
        return (fitting ?? main.MinBy(i => Math.Max(i.Width, i.Height)))?.Link;
    }

    private static bool IsAmazonImageHost(Uri url) =>
        url.IsAbsoluteUri
        && url.Scheme == Uri.UriSchemeHttps
        && ImageHostSuffixes.Any(suffix => url.Host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
}
