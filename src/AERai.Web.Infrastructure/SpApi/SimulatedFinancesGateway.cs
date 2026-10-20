using System.Globalization;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Finances gateway for <see cref="SpApiMode.Simulated"/>: defers the simulated orders (the same ones
/// <see cref="SimulatedOrdersGateway"/> makes up) until ten days after they were placed, Amazon's delivery-date
/// policy. Orders still inside those ten days are listed as held; older ones (up to <see cref="ReleasedHistory"/>
/// back) as released. Each carries a settlement id for the 14-day period it was sold in and its product. The JSON
/// has Amazon's shape and is parsed by <see cref="FinancesApiClient.ParsePage"/>, like a live response. Nothing
/// leaves the machine.
/// </summary>
/// <remarks>
/// The settlement ids are made up per period (<c>SIM-yyyyMMdd</c>) and do not match the simulated settlement
/// reports, so locally every group shows as a settlement not imported yet.
/// </remarks>
/// <param name="time">Clock (which orders are still held).</param>
internal sealed class SimulatedFinancesGateway(TimeProvider time) : IAmazonFinancesGateway
{
    /// <summary>Transactions per simulated page; small so a run spans several pages.</summary>
    internal const int PageSize = 10;

    /// <summary>Delivery a few days after the order, then Amazon's seven days.</summary>
    internal static readonly TimeSpan HeldFor = TimeSpan.FromDays(10);

    /// <summary>How far back released deferrals are made up, keeping a local run short.</summary>
    internal static readonly TimeSpan ReleasedHistory = TimeSpan.FromDays(42);

    private static readonly DateTimeOffset PeriodEpoch = new(2026, 1, 5, 1, 53, 0, TimeSpan.Zero);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public Task<DeferredTransactionPage> ListTransactionsPageAsync(
        Marketplace marketplace, DeferredTransactionListing listing, DateTimeOffset postedAfter, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var now = time.GetUtcNow();
        var releasedListing = listing == DeferredTransactionListing.Released;
        var earliest = releasedListing ? now - ReleasedHistory : now - HeldFor;
        var from = postedAfter > earliest ? postedAfter : earliest;
        var orders = SimulatedOrdersGateway.Slots(from, now)
            .Select(placed => (Placed: placed, Order: SimulatedOrdersGateway.Describe(marketplace, placed)))
            .Where(o => !o.Order.Cancelled && (o.Placed + HeldFor > now) != releasedListing)
            .ToList();

        var start = nextToken is null ? 0 : int.Parse(nextToken, NumberStyles.None, CultureInfo.InvariantCulture);
        var page = orders.Skip(start).Take(PageSize).ToList();
        var next = start + PageSize < orders.Count ? (start + PageSize).ToString(CultureInfo.InvariantCulture) : null;

        var body = new
        {
            payload = new
            {
                nextToken = next,
                transactions = page.Select(o =>
                {
                    // Roughly what is left after Amazon's fees.
                    var net = decimal.Round(o.Order.Total * 0.62m, 2);
                    return new
                    {
                        transactionId = $"SIM-{o.Order.OrderId}",
                        transactionType = "Shipment",
                        transactionStatus = FinancesApiClient.StatusOf(listing),
                        description = "Order Payment",
                        postedDate = o.Placed.AddDays(1),
                        totalAmount = new { currencyCode = marketplace.Currency, currencyAmount = net },
                        relatedIdentifiers = new[]
                        {
                            new { relatedIdentifierName = "ORDER_ID", relatedIdentifierValue = o.Order.OrderId },
                            new { relatedIdentifierName = "SETTLEMENT_ID", relatedIdentifierValue = SettlementIdFor(o.Placed.AddDays(1)) },
                        },
                        marketplaceDetails = new { marketplaceId = marketplace.MarketplaceId },
                        items = new[]
                        {
                            new
                            {
                                totalAmount = new { currencyCode = marketplace.Currency, currencyAmount = net },
                                breakdowns = new[] { new { breakdownType = "ProductCharges", breakdownAmount = new { currencyCode = marketplace.Currency, currencyAmount = o.Order.Total } } },
                                contexts = new[] { new { contextType = "ProductContext", sku = o.Order.Sku, asin = o.Order.Asin, quantityShipped = o.Order.Quantity } },
                            },
                        },
                        contexts = new object[]
                        {
                            new { contextType = "DeferredContext", deferralReason = "DD7", maturityDate = o.Placed + HeldFor },
                        },
                    };
                }),
            },
        };

        return Task.FromResult(FinancesApiClient.ParsePage(JsonSerializer.Serialize(body, JsonOptions), listing));
    }

    /// <summary>A made-up settlement id for the 14-day period an instant falls in.</summary>
    private static string SettlementIdFor(DateTimeOffset posted)
    {
        var periods = (int)Math.Floor((posted - PeriodEpoch).TotalDays / 14);
        return string.Create(CultureInfo.InvariantCulture, $"SIM-{PeriodEpoch.AddDays(periods * 14):yyyyMMdd}");
    }
}
