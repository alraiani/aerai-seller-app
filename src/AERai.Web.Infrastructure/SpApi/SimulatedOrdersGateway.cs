using System.Globalization;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Orders gateway for <see cref="SpApiMode.Simulated"/>: makes up a handful of orders per day inside the
/// requested window, a few per page so pagination is exercised locally. The JSON has Amazon's shape and
/// is parsed by <see cref="OrdersApiClient.ParsePage"/>, like a live response. Nothing leaves the machine.
/// </summary>
internal sealed class SimulatedOrdersGateway : IAmazonOrdersGateway
{
    /// <summary>Orders per simulated page; small so a run spans several pages.</summary>
    internal const int PageSize = 5;

    private const int OrdersPerDay = 6;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public Task<AmazonOrdersPage> SearchOrdersPageAsync(Marketplace marketplace, AmazonOrdersFilter filter, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(filter);

        var now = DateTimeOffset.UtcNow;
        var from = filter.CreatedAfter ?? filter.LastUpdatedAfter ?? now.AddDays(-1);
        var to = filter.CreatedBefore ?? now;

        var slots = Slots(from, to);
        var start = nextToken is null ? 0 : int.Parse(nextToken, NumberStyles.None, CultureInfo.InvariantCulture);
        var page = slots.Skip(start).Take(PageSize).ToList();
        var next = start + PageSize < slots.Count ? (start + PageSize).ToString(CultureInfo.InvariantCulture) : null;

        var body = new
        {
            orders = page.Select(placed => BuildOrder(marketplace, placed, now)),
            pagination = new { nextToken = next },
        };

        return Task.FromResult(OrdersApiClient.ParsePage(JsonSerializer.Serialize(body, JsonOptions), marketplace.Currency));
    }

    /// <summary>
    /// When the simulated orders in a window were placed: at fixed offsets within each UTC day, so repeated
    /// runs (and the simulated deferred transactions) see the same orders.
    /// </summary>
    /// <param name="from">Window start.</param>
    /// <param name="to">Window end (exclusive).</param>
    /// <returns>Placement times, oldest first.</returns>
    internal static List<DateTimeOffset> Slots(DateTimeOffset from, DateTimeOffset to)
    {
        var slots = new List<DateTimeOffset>();
        for (var day = from.UtcDateTime.Date; day <= to.UtcDateTime.Date; day = day.AddDays(1))
        {
            for (var n = 0; n < OrdersPerDay; n++)
            {
                var placed = new DateTimeOffset(day, TimeSpan.Zero).AddMinutes((n * 233) + 37);
                if (placed >= from && placed < to)
                {
                    slots.Add(placed);
                }
            }
        }

        return slots;
    }

    /// <summary>The simulated order placed at a slot: its id, product, total, and whether it was cancelled.</summary>
    /// <param name="marketplace">Marketplace.</param>
    /// <param name="placed">A time from <see cref="Slots"/>.</param>
    /// <returns>The order's id, its one product and quantity, item total, and whether it was cancelled.</returns>
    internal static (string OrderId, string Sku, string Asin, int Quantity, decimal Total, bool Cancelled) Describe(Marketplace marketplace, DateTimeOffset placed)
    {
        var seed = Seed(marketplace, placed);
        var (sku, asin, _, price) = SimulatedReportsGateway.Catalog[(int)(seed % (uint)SimulatedReportsGateway.Catalog.Length)];
        var quantity = 1 + (int)(seed % 3);
        var prefix = marketplace.Code switch { "US" => "111", "CA" => "701", _ => "203" };
        return (FormattableString.Invariant($"{prefix}-{(seed % 9_000_000) + 1_000_000}-{placed:HHmmss}"),
            sku,
            asin,
            quantity,
            price * (marketplace.Code == "UK" ? 0.8m : 1m) * quantity,
            seed % 17 == 0);
    }

    private static uint Seed(Marketplace marketplace, DateTimeOffset placed) =>
        Hash(FormattableString.Invariant($"{marketplace.Code}{placed:yyyyMMddHHmm}"));

    private static object BuildOrder(Marketplace marketplace, DateTimeOffset placed, DateTimeOffset now)
    {
        var seed = Seed(marketplace, placed);
        var (sku, asin, title, price) = SimulatedReportsGateway.Catalog[(int)(seed % (uint)SimulatedReportsGateway.Catalog.Length)];
        var quantity = 1 + (int)(seed % 3);
        var unitPrice = price * (marketplace.Code == "UK" ? 0.8m : 1m);
        var status = now - placed < TimeSpan.FromHours(6) ? "PENDING" : seed % 17 == 0 ? "CANCELLED" : "SHIPPED";

        return new
        {
            orderId = Describe(marketplace, placed).OrderId,
            createdTime = placed,
            lastUpdatedTime = placed.AddHours(2),
            salesChannel = new { marketplaceId = marketplace.MarketplaceId },
            fulfillment = new { fulfillmentStatus = status },
            proceeds = new { grandTotal = new { amount = (unitPrice * quantity).ToString("0.00", CultureInfo.InvariantCulture), currencyCode = marketplace.Currency } },
            orderItems = new[]
            {
                new
                {
                    orderItemId = "1",
                    quantityOrdered = quantity,
                    product = new
                    {
                        asin,
                        title,
                        sellerSku = sku,
                        price = new { unitPrice = new { amount = unitPrice.ToString("0.00", CultureInfo.InvariantCulture), currencyCode = marketplace.Currency } },
                    },
                },
            },
        };
    }

    // Stable across processes, unlike string.GetHashCode.
    private static uint Hash(string value)
    {
        var hash = 2166136261u;
        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }
}
