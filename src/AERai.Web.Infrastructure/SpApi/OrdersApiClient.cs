using System.Globalization;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Options;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Typed client for the SP-API Orders API (version 2026-01-01). Every call is tagged with its
/// operation name and region and sent through <see cref="SpApiPipelineHandler"/>.
/// </summary>
/// <remarks>
/// <c>searchOrders</c> returns each order with its line items inline, so there is no per-order
/// follow-up call as with the older v0 <c>getOrders</c> + <c>getOrderItems</c> pair.
/// </remarks>
/// <param name="http">HttpClient configured with the SP-API endpoint and pipeline handler.</param>
/// <param name="options">SP-API settings (regional endpoints).</param>
internal sealed class OrdersApiClient(HttpClient http, IOptions<SpApiOptions> options)
{
    private const string BasePath = "orders/2026-01-01";

    /// <summary>The optional sections of an order to request; the status lives in <c>FULFILLMENT</c>.</summary>
    internal const string IncludedData = "FULFILLMENT";

    /// <summary>Calls <c>searchOrders</c> for one page.</summary>
    /// <param name="marketplace">Marketplace to search; its region selects the endpoint and credentials.</param>
    /// <param name="filter">Which orders to return.</param>
    /// <param name="nextToken">Pagination token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, parsed and as raw JSON.</returns>
    /// <exception cref="ArgumentException">The filter has both or neither of <c>CreatedAfter</c> and <c>LastUpdatedAfter</c>.</exception>
    public async Task<AmazonOrdersPage> SearchOrdersAsync(Marketplace marketplace, AmazonOrdersFilter filter, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(filter);
        if (filter.CreatedAfter is null == filter.LastUpdatedAfter is null)
        {
            throw new ArgumentException("Exactly one of CreatedAfter and LastUpdatedAfter is required.", nameof(filter));
        }

        // Amazon wants the filter on every request, including later pages, so it is rebuilt each time.
        // Without includedData the response is only the basics (ids, times, items): no fulfillment
        // block, hence no order status. FULFILLMENT adds order.fulfillment.fulfillmentStatus.
        var query = $"marketplaceIds={Uri.EscapeDataString(marketplace.MarketplaceId)}&includedData={IncludedData}";
        if (filter.CreatedAfter is { } createdAfter)
        {
            query += $"&createdAfter={Uri.EscapeDataString(Iso8601(createdAfter))}";
        }

        if (filter.LastUpdatedAfter is { } lastUpdatedAfter)
        {
            query += $"&lastUpdatedAfter={Uri.EscapeDataString(Iso8601(lastUpdatedAfter))}";
        }

        if (filter.CreatedBefore is { } createdBefore)
        {
            query += $"&createdBefore={Uri.EscapeDataString(Iso8601(createdBefore))}";
        }

        if (!string.IsNullOrEmpty(nextToken))
        {
            query += $"&paginationToken={Uri.EscapeDataString(nextToken)}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(options.Value.EndpointFor(marketplace.Region), $"{BasePath}/orders?{query}"));
        request.Options.Set(SpApiOperation.RegionKey, marketplace.Region);
        request.Options.Set(SpApiOperation.OptionKey, SpApiOperation.SearchOrders);

        using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            // SP-API error bodies ({"errors":[{code,message}]}) contain no secrets and help diagnose the call.
            throw new HttpRequestException(
                $"SP-API {SpApiOperation.SearchOrders} failed with HTTP {(int)response.StatusCode}: {(body.Length <= 500 ? body : body[..500])}",
                null,
                response.StatusCode);
        }

        return ParsePage(body, marketplace.Currency);
    }

    /// <summary>Parses a <c>searchOrders</c> response body (see <see cref="OrdersPageParser"/>).</summary>
    /// <param name="json">The response body.</param>
    /// <param name="fallbackCurrency">Currency used when Amazon sends none for an order.</param>
    /// <returns>The page.</returns>
    public static AmazonOrdersPage ParsePage(string json, string? fallbackCurrency = null) => OrdersPageParser.Parse(json, fallbackCurrency);

    /// <summary>Puts Amazon's status in the spelling the views use (see <see cref="OrdersPageParser.NormalizeStatus"/>).</summary>
    /// <param name="fulfillmentStatus">Amazon's fulfillment status, if any.</param>
    /// <returns>The normalized status.</returns>
    internal static string NormalizeStatus(string? fulfillmentStatus) => OrdersPageParser.NormalizeStatus(fulfillmentStatus);

    // Amazon rejects fractional seconds and non-UTC offsets, so send whole-second UTC.
    private static string Iso8601(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
