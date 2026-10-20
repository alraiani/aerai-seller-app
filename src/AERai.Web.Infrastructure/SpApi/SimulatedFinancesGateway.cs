using System.Globalization;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Finances gateway for <see cref="SpApiMode.Simulated"/>: defers the simulated orders (the same ones
/// <see cref="SimulatedOrdersGateway"/> makes up) until ten days after they were placed, Amazon's delivery-date
/// policy, a few per page so pagination is exercised locally. The JSON has Amazon's shape and is parsed by
/// <see cref="FinancesApiClient.ParsePage"/>, like a live response. Nothing leaves the machine.
/// </summary>
/// <param name="time">Clock (which orders are still held).</param>
internal sealed class SimulatedFinancesGateway(TimeProvider time) : IAmazonFinancesGateway
{
    /// <summary>Transactions per simulated page; small so a run spans several pages.</summary>
    internal const int PageSize = 10;

    /// <summary>Delivery a few days after the order, then Amazon's seven days.</summary>
    internal static readonly TimeSpan HeldFor = TimeSpan.FromDays(10);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public Task<DeferredTransactionPage> ListTransactionsPageAsync(Marketplace marketplace, DateTimeOffset postedAfter, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var now = time.GetUtcNow();
        var from = postedAfter > now - HeldFor ? postedAfter : now - HeldFor;
        var held = SimulatedOrdersGateway.Slots(from, now)
            .Select(placed => (Placed: placed, Order: SimulatedOrdersGateway.Describe(marketplace, placed)))
            .Where(o => !o.Order.Cancelled && o.Placed + HeldFor > now)
            .ToList();

        var start = nextToken is null ? 0 : int.Parse(nextToken, NumberStyles.None, CultureInfo.InvariantCulture);
        var page = held.Skip(start).Take(PageSize).ToList();
        var next = start + PageSize < held.Count ? (start + PageSize).ToString(CultureInfo.InvariantCulture) : null;

        var body = new
        {
            payload = new
            {
                nextToken = next,
                transactions = page.Select(o => new
                {
                    transactionId = $"SIM-{o.Order.OrderId}",
                    transactionType = "Shipment",
                    transactionStatus = FinancesApiClient.DeferredStatus,
                    description = "Order Payment",
                    postedDate = o.Placed.AddDays(1),
                    // Roughly what is left after Amazon's fees.
                    totalAmount = new { currencyCode = marketplace.Currency, currencyAmount = decimal.Round(o.Order.Total * 0.62m, 2) },
                    relatedIdentifiers = new[] { new { relatedIdentifierName = "ORDER_ID", relatedIdentifierValue = o.Order.OrderId } },
                    marketplaceDetails = new { marketplaceId = marketplace.MarketplaceId },
                    contexts = new object[]
                    {
                        new { contextType = "DeferredContext", deferralReason = "DD7", maturityDate = o.Placed + HeldFor },
                    },
                }),
            },
        };

        return Task.FromResult(FinancesApiClient.ParsePage(JsonSerializer.Serialize(body, JsonOptions)));
    }
}
