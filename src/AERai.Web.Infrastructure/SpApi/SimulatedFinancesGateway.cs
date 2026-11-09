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

    /// <inheritdoc/>
    /// <remarks>
    /// Every simulated order (not cancelled) becomes a <c>Shipment</c> posted the day after it was placed, with
    /// principal, commission (15%), and an FBA fee per unit, held for <see cref="HeldFor"/>. Every 20th order is
    /// refunded four days later (Amazon keeps a fifth of the commission), and the first of each month carries a
    /// SKU-less subscription fee, so the ledger has the kinds of amounts the profit page sorts.
    /// </remarks>
    public Task<FinancialTransactionPage> ListLedgerPageAsync(Marketplace marketplace, LedgerQuery query, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        ArgumentNullException.ThrowIfNull(query);

        var now = time.GetUtcNow();
        var currency = marketplace.Currency;
        object Money(decimal amount) => new { currencyCode = currency, currencyAmount = amount };
        object Breakdown(string type, decimal amount, params object[] children) =>
            new { breakdownType = type, breakdownAmount = Money(amount), breakdowns = children };

        var all = new List<(DateTimeOffset Posted, bool Held, object Body)>();

        // Orders placed a few days before the window can still post (or be refunded) inside it.
        foreach (var placed in SimulatedOrdersGateway.Slots(query.PostedAfter.AddDays(-5), query.PostedBefore))
        {
            var order = SimulatedOrdersGateway.Describe(marketplace, placed);
            if (order.Cancelled)
            {
                continue;
            }

            var posted = placed.AddDays(1);
            var commission = -decimal.Round(order.Total * 0.15m, 2);
            var fbaFee = -3.22m * order.Quantity;
            var product = new { contextType = "ProductContext", sku = order.Sku, asin = order.Asin, quantityShipped = order.Quantity };
            all.Add((posted, placed + HeldFor > now, new
            {
                transactionId = $"SIM-L-{order.OrderId}",
                transactionType = "Shipment",
                transactionStatus = placed + HeldFor > now ? "DEFERRED" : "RELEASED",
                description = "Order Payment",
                postedDate = posted,
                totalAmount = Money(order.Total + commission + fbaFee),
                relatedIdentifiers = Related(order.OrderId, posted),
                items = new[]
                {
                    new
                    {
                        totalAmount = Money(order.Total + commission + fbaFee),
                        breakdowns = new[]
                        {
                            Breakdown("ProductCharges", order.Total, Breakdown("Principal", order.Total)),
                            Breakdown("AmazonFees", commission + fbaFee, Breakdown("Commission", commission), Breakdown("FBAPerUnitFulfillmentFee", fbaFee)),
                        },
                        contexts = new object[] { product },
                    },
                },
            }));

            if (placed.Ticks / TimeSpan.TicksPerHour % 20 == 0)
            {
                var refunded = placed.AddDays(5);
                var commissionBack = -decimal.Round(commission * 0.8m, 2);
                all.Add((refunded, false, new
                {
                    transactionId = $"SIM-R-{order.OrderId}",
                    transactionType = "Refund",
                    transactionStatus = "RELEASED",
                    description = "Refund",
                    postedDate = refunded,
                    totalAmount = Money(-order.Total + commissionBack),
                    relatedIdentifiers = Related(order.OrderId, refunded),
                    items = new[]
                    {
                        new
                        {
                            totalAmount = Money(-order.Total + commissionBack),
                            breakdowns = new[]
                            {
                                Breakdown("ProductCharges", -order.Total, Breakdown("Principal", -order.Total)),
                                Breakdown("AmazonFees", commissionBack, Breakdown("Commission", commissionBack)),
                            },
                            contexts = new object[] { product with { quantityShipped = 0 } },
                        },
                    },
                }));
            }
        }

        for (var month = new DateTimeOffset(query.PostedAfter.Year, query.PostedAfter.Month, 1, 8, 0, 0, TimeSpan.Zero); month < query.PostedBefore; month = month.AddMonths(1))
        {
            all.Add((month, false, new
            {
                transactionId = string.Create(CultureInfo.InvariantCulture, $"SIM-F-{marketplace.MarketplaceId}-{month:yyyyMM}"),
                transactionType = "ServiceFee",
                transactionStatus = "RELEASED",
                description = "Subscription",
                postedDate = month,
                totalAmount = Money(-39.99m),
                relatedIdentifiers = new[] { new { relatedIdentifierName = "SETTLEMENT_ID", relatedIdentifierValue = SettlementIdFor(month) } },
                breakdowns = new[] { Breakdown("ServiceFees", -39.99m, Breakdown("Subscription", -39.99m)) },
            }));
        }

        var matching = all
            .Where(t => t.Posted >= query.PostedAfter && t.Posted < query.PostedBefore && t.Posted <= now && (!query.HeldOnly || t.Held))
            .OrderBy(t => t.Posted)
            .Select(t => t.Body)
            .ToList();
        var start = nextToken is null ? 0 : int.Parse(nextToken, NumberStyles.None, CultureInfo.InvariantCulture);
        var next = start + PageSize < matching.Count ? (start + PageSize).ToString(CultureInfo.InvariantCulture) : null;
        var body = new { payload = new { nextToken = next, transactions = matching.Skip(start).Take(PageSize) } };
        return Task.FromResult(LedgerPageParser.Parse(JsonSerializer.Serialize(body, JsonOptions)));
    }

    private static object[] Related(string orderId, DateTimeOffset posted) =>
    [
        new { relatedIdentifierName = "ORDER_ID", relatedIdentifierValue = orderId },
        new { relatedIdentifierName = "SETTLEMENT_ID", relatedIdentifierValue = SettlementIdFor(posted) },
    ];

    /// <summary>A made-up settlement id for the 14-day period an instant falls in.</summary>
    private static string SettlementIdFor(DateTimeOffset posted)
    {
        var periods = (int)Math.Floor((posted - PeriodEpoch).TotalDays / 14);
        return string.Create(CultureInfo.InvariantCulture, $"SIM-{PeriodEpoch.AddDays(periods * 14):yyyyMMdd}");
    }
}
