using System.Net.Http.Json;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Typed client for the SP-API Catalog Items API (version 2022-04-01). Every call is tagged with its
/// operation name and region and sent through <see cref="SpApiPipelineHandler"/>.
/// </summary>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="options">SP-API settings (regional endpoints).</param>
internal sealed class CatalogItemsApiClient(HttpClient http, IOptions<SpApiOptions> options)
{
    /// <summary>Most identifiers <c>searchCatalogItems</c> accepts in one call.</summary>
    public const int MaxIdentifiersPerCall = 20;

    private const string BasePath = "catalog/2022-04-01";

    /// <summary>
    /// Calls <c>searchCatalogItems</c> by ASIN with <c>includedData=images</c>, at most
    /// <see cref="MaxIdentifiersPerCall"/> ASINs per call.
    /// </summary>
    /// <param name="marketplace">Marketplace whose catalog to read.</param>
    /// <param name="asins">Up to <see cref="MaxIdentifiersPerCall"/> ASINs.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The items Amazon found (unknown ASINs are simply absent).</returns>
    public async Task<IReadOnlyList<CatalogItem>> SearchByAsinAsync(Marketplace marketplace, IReadOnlyCollection<string> asins, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(asins);
        if (asins.Count is 0 or > MaxIdentifiersPerCall)
        {
            throw new ArgumentOutOfRangeException(nameof(asins), asins.Count, $"Pass 1 to {MaxIdentifiersPerCall} ASINs.");
        }

        var query = $"identifiers={Uri.EscapeDataString(string.Join(',', asins))}&identifiersType=ASIN" +
                    $"&marketplaceIds={Uri.EscapeDataString(marketplace.MarketplaceId)}&includedData=images&pageSize={asins.Count}";

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.Value.EndpointFor(marketplace.Region), $"{BasePath}/items?{query}"));
        request.Options.Set(SpApiOperation.RegionKey, marketplace.Region);
        request.Options.Set(SpApiOperation.OptionKey, SpApiOperation.SearchCatalogItems);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // SP-API error bodies ({"errors":[{code,message}]}) contain no secrets and help diagnose the call.
            var detail = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new HttpRequestException(
                $"SP-API {SpApiOperation.SearchCatalogItems} failed with HTTP {(int)response.StatusCode}: {(detail.Length <= 500 ? detail : detail[..500])}",
                null,
                response.StatusCode);
        }

        var body = await response.Content.ReadFromJsonAsync<SearchResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.SearchCatalogItems} returned an empty body.");
        return body.Items ?? [];
    }

    private sealed record SearchResponse(IReadOnlyList<CatalogItem>? Items);
}

/// <summary>A catalog item with its pictures, as returned by <c>searchCatalogItems</c>.</summary>
/// <param name="Asin">ASIN.</param>
/// <param name="Images">Pictures per marketplace.</param>
internal sealed record CatalogItem(string Asin, IReadOnlyList<CatalogItemImages>? Images);

/// <summary>A catalog item's pictures in one marketplace.</summary>
/// <param name="MarketplaceId">Marketplace id.</param>
/// <param name="Images">The pictures, each in several sizes.</param>
internal sealed record CatalogItemImages(string MarketplaceId, IReadOnlyList<CatalogImage>? Images);

/// <summary>One size of one catalog picture.</summary>
/// <param name="Variant">MAIN for the listing's main picture, PT01… for the others.</param>
/// <param name="Link">Where the picture is served.</param>
/// <param name="Height">Height in pixels.</param>
/// <param name="Width">Width in pixels.</param>
internal sealed record CatalogImage(string Variant, Uri Link, int Height, int Width);
