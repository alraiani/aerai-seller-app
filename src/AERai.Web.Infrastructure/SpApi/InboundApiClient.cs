using System.Text.Json;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Typed client for the SP-API Fulfillment Inbound API (version 2024-03-20): the seller's Send to Amazon plans, their
/// shipments, and each shipment's SKUs. Every call is tagged with its operation name and region and sent through
/// <see cref="SpApiPipelineHandler"/>.
/// </summary>
/// <remarks>
/// Only reads are used. Plans are listed most recently updated first, so a run stops paging once it reaches plans
/// older than its window.
/// </remarks>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="options">SP-API settings (regional endpoints).</param>
internal sealed class InboundApiClient(HttpClient http, IOptions<SpApiOptions> options)
{
    /// <summary>Largest page <c>listInboundPlans</c> returns.</summary>
    public const int MaxPlansPerPage = 30;

    /// <summary>Largest page <c>listShipmentItems</c> returns.</summary>
    public const int MaxItemsPerPage = 1000;

    private const string BasePath = "inbound/fba/2024-03-20/inboundPlans";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Calls <c>listInboundPlans</c>, most recently updated first, for one page.</summary>
    /// <param name="marketplace">Marketplace being synced; its region selects the endpoint and credentials.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, parsed and as raw JSON.</returns>
    public async Task<InboundPlanPage> ListPlansAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken)
    {
        var query = $"pageSize={MaxPlansPerPage}&sortBy=LAST_UPDATED_TIME&sortOrder=DESC";
        if (!string.IsNullOrEmpty(nextToken))
        {
            query += $"&paginationToken={Uri.EscapeDataString(nextToken)}";
        }

        var body = await GetAsync(marketplace, $"{BasePath}?{query}", SpApiOperation.ListInboundPlans, cancellationToken).ConfigureAwait(false);
        return ParsePlansPage(body);
    }

    /// <summary>Calls <c>getInboundPlan</c> for the plan's shipments.</summary>
    /// <param name="marketplace">Marketplace being synced.</param>
    /// <param name="inboundPlanId">The plan.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The plan's shipment ids, parsed and as raw JSON.</returns>
    public async Task<InboundPlanShipments> GetPlanAsync(Marketplace marketplace, string inboundPlanId, CancellationToken cancellationToken)
    {
        var body = await GetAsync(marketplace, $"{BasePath}/{Uri.EscapeDataString(inboundPlanId)}", SpApiOperation.GetInboundPlan, cancellationToken).ConfigureAwait(false);
        return ParsePlan(body, inboundPlanId);
    }

    /// <summary>Calls <c>getShipment</c>.</summary>
    /// <param name="marketplace">Marketplace being synced.</param>
    /// <param name="inboundPlanId">The plan.</param>
    /// <param name="shipmentId">Amazon's internal shipment id.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The shipment, parsed and as raw JSON.</returns>
    public async Task<InboundShipmentDetail> GetShipmentAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, CancellationToken cancellationToken)
    {
        var path = $"{BasePath}/{Uri.EscapeDataString(inboundPlanId)}/shipments/{Uri.EscapeDataString(shipmentId)}";
        var body = await GetAsync(marketplace, path, SpApiOperation.GetInboundShipment, cancellationToken).ConfigureAwait(false);
        return ParseShipment(body, shipmentId);
    }

    /// <summary>Calls <c>listShipmentItems</c> for one page.</summary>
    /// <param name="marketplace">Marketplace being synced.</param>
    /// <param name="inboundPlanId">The plan.</param>
    /// <param name="shipmentId">Amazon's internal shipment id.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, parsed and as raw JSON.</returns>
    public async Task<InboundShipmentItemsPage> ListShipmentItemsAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, string? nextToken, CancellationToken cancellationToken)
    {
        var query = $"pageSize={MaxItemsPerPage}";
        if (!string.IsNullOrEmpty(nextToken))
        {
            query += $"&paginationToken={Uri.EscapeDataString(nextToken)}";
        }

        var path = $"{BasePath}/{Uri.EscapeDataString(inboundPlanId)}/shipments/{Uri.EscapeDataString(shipmentId)}/items?{query}";
        var body = await GetAsync(marketplace, path, SpApiOperation.ListInboundShipmentItems, cancellationToken).ConfigureAwait(false);
        return ParseItemsPage(body);
    }

    /// <summary>Parses a <c>listInboundPlans</c> response body.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The page; plans without an id are dropped.</returns>
    /// <exception cref="InvalidOperationException">The body is empty.</exception>
    public static InboundPlanPage ParsePlansPage(string json)
    {
        var response = JsonSerializer.Deserialize<ListPlansResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.ListInboundPlans} returned an empty body.");
        var plans = (response.InboundPlans ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p.InboundPlanId))
            .Select(p => new InboundPlanSummary(
                p.InboundPlanId!.Trim(), // Non-null: plans without an id were filtered out just above.
                p.Name,
                p.Status ?? string.Empty,
                p.CreatedAt,
                p.LastUpdatedAt,
                p.MarketplaceIds ?? []))
            .ToList();
        return new InboundPlanPage(plans, response.Pagination?.NextToken, json);
    }

    /// <summary>Parses a <c>getInboundPlan</c> response body.</summary>
    /// <param name="json">The response body.</param>
    /// <param name="inboundPlanId">The plan asked for (used when the body omits it).</param>
    /// <returns>The plan's shipment ids.</returns>
    /// <exception cref="InvalidOperationException">The body is empty.</exception>
    public static InboundPlanShipments ParsePlan(string json, string inboundPlanId)
    {
        var response = JsonSerializer.Deserialize<InboundPlanResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.GetInboundPlan} returned an empty body.");
        var ids = (response.Shipments ?? [])
            .Select(s => s.ShipmentId?.Trim())
            .OfType<string>()
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        return new InboundPlanShipments(response.InboundPlanId ?? inboundPlanId, ids, json);
    }

    /// <summary>Parses a <c>getShipment</c> response body.</summary>
    /// <param name="json">The response body.</param>
    /// <param name="shipmentId">The shipment asked for (used when the body omits it).</param>
    /// <returns>The shipment; a blank confirmation id reads as none.</returns>
    /// <exception cref="InvalidOperationException">The body is empty.</exception>
    public static InboundShipmentDetail ParseShipment(string json, string shipmentId)
    {
        var response = JsonSerializer.Deserialize<ShipmentResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.GetInboundShipment} returned an empty body.");
        var confirmation = string.IsNullOrWhiteSpace(response.ShipmentConfirmationId) ? null : response.ShipmentConfirmationId.Trim();
        return new InboundShipmentDetail(response.ShipmentId ?? shipmentId, confirmation, response.Name, response.Status ?? string.Empty, json);
    }

    /// <summary>Parses a <c>listShipmentItems</c> response body.</summary>
    /// <param name="json">The response body.</param>
    /// <returns>The page; items without a SKU are dropped and negative quantities read as zero.</returns>
    /// <exception cref="InvalidOperationException">The body is empty.</exception>
    public static InboundShipmentItemsPage ParseItemsPage(string json)
    {
        var response = JsonSerializer.Deserialize<ListItemsResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException($"SP-API {SpApiOperation.ListInboundShipmentItems} returned an empty body.");
        var items = (response.Items ?? [])
            .Where(i => !string.IsNullOrWhiteSpace(i.Msku))
            .Select(i => new AmazonInboundItem(i.Msku!.Trim(), Math.Max(0, i.Quantity ?? 0))) // Non-null: filtered just above.
            .ToList();
        return new InboundShipmentItemsPage(items, response.Pagination?.NextToken, json);
    }

    private async Task<string> GetAsync(Marketplace marketplace, string pathAndQuery, string operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.Value.EndpointFor(marketplace.Region), pathAndQuery));
        request.Options.Set(SpApiOperation.RegionKey, marketplace.Region);
        request.Options.Set(SpApiOperation.OptionKey, operation);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // SP-API error bodies ({"errors":[{code,message}]}) contain no secrets and help diagnose the call.
            throw new HttpRequestException(
                $"SP-API {operation} failed with HTTP {(int)response.StatusCode}: {(body.Length <= 500 ? body : body[..500])}",
                null,
                response.StatusCode);
        }

        return body;
    }

    private sealed record Pagination(string? NextToken);

    private sealed record ListPlansResponse(IReadOnlyList<PlanSummary>? InboundPlans, Pagination? Pagination);

    private sealed record PlanSummary(string? InboundPlanId, string? Name, string? Status, DateTimeOffset? CreatedAt, DateTimeOffset? LastUpdatedAt, IReadOnlyList<string>? MarketplaceIds);

    private sealed record InboundPlanResponse(string? InboundPlanId, IReadOnlyList<ShipmentSummary>? Shipments);

    private sealed record ShipmentSummary(string? ShipmentId, string? Status);

    private sealed record ShipmentResponse(string? ShipmentId, string? ShipmentConfirmationId, string? Name, string? Status);

    private sealed record ListItemsResponse(IReadOnlyList<Item>? Items, Pagination? Pagination);

    private sealed record Item(string? Msku, int? Quantity);
}
