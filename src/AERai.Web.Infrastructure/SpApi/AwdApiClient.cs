using System.Text.Json;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Typed client for the SP-API Amazon Warehousing and Distribution (AWD) API (version 2024-05-09).
/// Every call is tagged with its operation name and region and sent through <see cref="SpApiPipelineHandler"/>.
/// </summary>
/// <remarks>
/// Unlike FBA inventory, which comes from the Reports API, <c>listInventory</c> is a plain paginated
/// GET: no report to request, wait for, or download.
/// </remarks>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="options">SP-API settings (regional endpoints).</param>
internal sealed class AwdApiClient(HttpClient http, IOptions<SpApiOptions> options)
{
    /// <summary>Largest page <c>listInventory</c> returns.</summary>
    public const int MaxResultsPerPage = 200;

    private const string BasePath = "awd/2024-05-09";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Calls <c>listInventory</c> with <c>details=SHOW</c> (which adds the distributable and
    /// replenishment quantities) for one page.
    /// </summary>
    /// <param name="marketplace">Marketplace being synced; its region selects the endpoint and credentials.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, parsed and as raw JSON.</returns>
    public async Task<AwdInventoryPage> ListInventoryAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var query = $"details=SHOW&sortOrder=ASCENDING&maxResults={MaxResultsPerPage}";
        if (!string.IsNullOrEmpty(nextToken))
        {
            query += $"&nextToken={Uri.EscapeDataString(nextToken)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.Value.EndpointFor(marketplace.Region), $"{BasePath}/inventory?{query}"));
        request.Options.Set(SpApiOperation.RegionKey, marketplace.Region);
        request.Options.Set(SpApiOperation.OptionKey, SpApiOperation.ListAwdInventory);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // SP-API error bodies ({"errors":[{code,message}]}) contain no secrets and help diagnose the call.
            throw new HttpRequestException(
                $"SP-API {SpApiOperation.ListAwdInventory} failed with HTTP {(int)response.StatusCode}: {(body.Length <= 500 ? body : body[..500])}",
                null,
                response.StatusCode);
        }

        return ParsePage(body);
    }

    /// <summary>Parses a <c>listInventory</c> response body.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The page; missing details count as zero.</returns>
    /// <exception cref="InvalidOperationException">The body is empty or not a listing.</exception>
    public static AwdInventoryPage ParsePage(string json)
    {
        var response = JsonSerializer.Deserialize<ListInventoryResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.ListAwdInventory} returned an empty body.");
        var items = (response.Inventory ?? [])
            .Where(e => !string.IsNullOrWhiteSpace(e.Sku))
            .Select(e => new AwdInventoryItem(
                e.Sku!.Trim(), // Non-null: entries without a SKU were filtered out just above.
                e.TotalOnhandQuantity ?? 0,
                e.TotalInboundQuantity ?? 0,
                e.InventoryDetails?.AvailableDistributableQuantity ?? 0,
                e.InventoryDetails?.ReservedDistributableQuantity ?? 0,
                e.InventoryDetails?.ReplenishmentQuantity ?? 0))
            .ToList();
        return new AwdInventoryPage(items, response.NextToken, json);
    }

    private sealed record ListInventoryResponse(IReadOnlyList<InventorySummary>? Inventory, string? NextToken);

    private sealed record InventorySummary(string? Sku, int? TotalOnhandQuantity, int? TotalInboundQuantity, InventoryDetails? InventoryDetails);

    private sealed record InventoryDetails(int? AvailableDistributableQuantity, int? ReservedDistributableQuantity, int? ReplenishmentQuantity);
}
