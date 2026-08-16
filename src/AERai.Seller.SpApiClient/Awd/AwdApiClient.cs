using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AERai.Seller.SpApiClient.Awd;

/// <summary>
/// Typed client for the Amazon Warehousing and Distribution (AWD) API (2024-05-09). All calls
/// route through <see cref="SpApiRequestPipeline"/>. Unlike FBA inventory (pulled via the Reports
/// API), <c>listInventory</c> is a plain paginated GET — no create/poll/download cycle.
/// </summary>
public sealed class AwdApiClient(SpApiRequestPipeline pipeline)
{
    /// <summary>Amazon's documented max page size for listInventory.</summary>
    public const int MaxResultsPerPage = 200;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <param name="sku">Optional filter to a single SKU; omit to list all.</param>
    /// <param name="nextToken">Pagination token from a previous call's result, or null for the first page.</param>
    /// <param name="maxResults">Page size, 1-200.</param>
    public async Task<AwdListInventoryResult> ListInventoryAsync(
        string? sku, string? nextToken, int maxResults, CancellationToken cancellationToken)
    {
        using var response = await pipeline.SendAsync(
            "Awd.ListInventory",
            host =>
            {
                var query = $"details=SHOW&sortOrder=ASCENDING&maxResults={maxResults}";
                if (!string.IsNullOrEmpty(sku)) query += $"&sku={Uri.EscapeDataString(sku)}";
                if (!string.IsNullOrEmpty(nextToken)) query += $"&nextToken={Uri.EscapeDataString(nextToken)}";
                return new HttpRequestMessage(HttpMethod.Get, $"{host}/awd/2024-05-09/inventory?{query}");
            },
            cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<ListInventoryResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("listInventory response was empty.");

        var items = (result.Inventory ?? []).Select(MapToResult).ToList();
        return new AwdListInventoryResult(items, result.NextToken);
    }

    private static AwdInventoryItem MapToResult(InventorySummaryEntry entry) => new(
        Sku: entry.Sku,
        TotalOnhandQuantity: entry.TotalOnhandQuantity,
        TotalInboundQuantity: entry.TotalInboundQuantity,
        AvailableDistributableQuantity: entry.InventoryDetails?.AvailableDistributableQuantity ?? 0,
        ReservedDistributableQuantity: entry.InventoryDetails?.ReservedDistributableQuantity ?? 0,
        ReplenishmentQuantity: entry.InventoryDetails?.ReplenishmentQuantity ?? 0);

    public sealed record AwdListInventoryResult(IReadOnlyList<AwdInventoryItem> Inventory, string? NextToken);

    public sealed record AwdInventoryItem(
        string Sku,
        int TotalOnhandQuantity,
        int TotalInboundQuantity,
        int AvailableDistributableQuantity,
        int ReservedDistributableQuantity,
        int ReplenishmentQuantity);

    private sealed record ListInventoryResponse(
        IReadOnlyList<InventorySummaryEntry>? Inventory,
        [property: JsonPropertyName("nextToken")] string? NextToken);

    private sealed record InventorySummaryEntry(
        string Sku,
        int TotalOnhandQuantity,
        int TotalInboundQuantity,
        [property: JsonPropertyName("inventoryDetails")] InventoryDetailsEntry? InventoryDetails);

    private sealed record InventoryDetailsEntry(
        int AvailableDistributableQuantity,
        int ReservedDistributableQuantity,
        int ReplenishmentQuantity);
}
