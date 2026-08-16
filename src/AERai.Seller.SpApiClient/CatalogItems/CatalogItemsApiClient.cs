using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AERai.Seller.SpApiClient.CatalogItems;

/// <summary>
/// Typed client for the SP-API Catalog Items model (2022-04-01). All calls route through
/// <see cref="SpApiRequestPipeline"/>. Amazon caps <c>searchCatalogItems</c> at 20 identifiers
/// per call, so <see cref="SearchCatalogItemsAsync"/> handles a single batch — callers needing
/// more ASINs than that must chunk and call it repeatedly.
/// </summary>
public sealed class CatalogItemsApiClient(SpApiRequestPipeline pipeline, ICredentialStore credentialStore)
{
    public const int MaxIdentifiersPerRequest = 20;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <param name="asins">Up to <see cref="MaxIdentifiersPerRequest"/> ASINs to look up.</param>
    public async Task<IReadOnlyList<CatalogItemResult>> SearchCatalogItemsAsync(
        IReadOnlyList<string> asins, CancellationToken cancellationToken)
    {
        if (asins.Count == 0) return [];
        if (asins.Count > MaxIdentifiersPerRequest)
            throw new ArgumentException($"At most {MaxIdentifiersPerRequest} ASINs per call, got {asins.Count}.", nameof(asins));

        var credentials = await credentialStore.GetCredentialsAsync(cancellationToken)
            ?? throw new InvalidOperationException("No SP-API credentials configured. Set them in Settings first.");

        using var response = await pipeline.SendAsync(
            "CatalogItems.SearchCatalogItems",
            host =>
            {
                var query = $"identifiers={Uri.EscapeDataString(string.Join(",", asins))}" +
                            "&identifiersType=ASIN" +
                            $"&marketplaceIds={Uri.EscapeDataString(credentials.MarketplaceId)}" +
                            "&includedData=summaries,images,relationships";
                return new HttpRequestMessage(HttpMethod.Get, $"{host}/catalog/2022-04-01/items?{query}");
            },
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<SearchCatalogItemsResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("searchCatalogItems response was empty.");

        return result.Items.Select(MapToResult).ToList();
    }

    private static CatalogItemResult MapToResult(CatalogItemEntry item)
    {
        var summary = item.Summaries?.FirstOrDefault();
        var mainImage = item.Images?.FirstOrDefault()?.Images?
            .FirstOrDefault(i => i.Variant == "MAIN") ?? item.Images?.FirstOrDefault()?.Images?.FirstOrDefault();
        var parentAsin = item.Relationships?
            .SelectMany(r => r.Relationships ?? [])
            .SelectMany(r => r.ParentAsins ?? [])
            .FirstOrDefault();

        return new CatalogItemResult(
            Asin: item.Asin,
            ParentAsin: parentAsin,
            Title: summary?.ItemName,
            Brand: summary?.BrandName,
            ImageUrl: mainImage?.Link);
    }

    public sealed record CatalogItemResult(string Asin, string? ParentAsin, string? Title, string? Brand, string? ImageUrl);

    private sealed record SearchCatalogItemsResponse(IReadOnlyList<CatalogItemEntry> Items);

    private sealed record CatalogItemEntry(
        string Asin,
        IReadOnlyList<ItemSummary>? Summaries,
        IReadOnlyList<ItemImageSet>? Images,
        IReadOnlyList<ItemRelationshipSet>? Relationships);

    private sealed record ItemSummary(
        [property: JsonPropertyName("itemName")] string? ItemName,
        [property: JsonPropertyName("brandName")] string? BrandName);

    private sealed record ItemImageSet(IReadOnlyList<ItemImage>? Images);

    private sealed record ItemImage(string? Variant, string? Link);

    private sealed record ItemRelationshipSet(IReadOnlyList<ItemRelationship>? Relationships);

    private sealed record ItemRelationship(
        [property: JsonPropertyName("parentAsins")] IReadOnlyList<string>? ParentAsins,
        string? Type);
}
