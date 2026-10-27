using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Development-only <see cref="IAmazonReportsGateway"/> that generates files in the same layout as
/// the real SP-API reports, so schedules, blob landing, staging, and promotion can be exercised
/// end to end without Amazon credentials. Never registered unless <c>SpApi:Mode</c> is Simulated.
/// </summary>
/// <param name="clock">Clock for generated timestamps.</param>
internal sealed class SimulatedReportsGateway(TimeProvider clock) : IAmazonReportsGateway
{
    /// <summary>Simulated settlement period length (Amazon pays out roughly every 14 days).</summary>
    private static readonly TimeSpan SettlementPeriod = TimeSpan.FromDays(14);

    private static readonly DateTimeOffset SettlementAnchor = new(2026, 1, 1, 7, 0, 0, TimeSpan.Zero);

    /// <summary>The simulated product range, shared with <see cref="SimulatedAwdGateway"/> so AWD stock is for the same SKUs.</summary>
    internal static readonly (string Sku, string Asin, string Title, decimal Price)[] Catalog =
    [
        ("AER-MAT-BLK", "B0C1MAT001", "AERai Yoga Mat - Black", 29.99m),
        ("AER-MAT-BLU", "B0C1MAT002", "AERai Yoga Mat - Blue", 29.99m),
        ("AER-BLK-2PK", "B0C1BLK010", "AERai Yoga Block 2-Pack", 17.49m),
        ("AER-STRAP", "B0C1STR020", "AERai Stretch Strap", 9.99m),
        ("AER-TOWEL", "B0C1TWL030", "AERai Grip Towel", 21.00m),
    ];

    /// <summary>Document specs keyed by document id (the simulator's stand-in for S3).</summary>
    /// <summary>
    /// Local price level and order-id prefix per marketplace, so each one's simulated data looks like
    /// its own (CAD prices are higher, GBP lower) and order ids never collide across marketplaces.
    /// </summary>
    private static readonly Dictionary<string, (decimal PriceFactor, string OrderPrefix, double Volume)> Profiles = new(StringComparer.Ordinal)
    {
        [MarketplaceIds.UnitedStates] = (1.00m, "114", 1.0),
        [MarketplaceIds.Canada] = (1.35m, "702", 0.35),
        [MarketplaceIds.UnitedKingdom] = (0.80m, "203", 0.5),
    };

    /// <summary>
    /// Who gets paid out in each region, as Amazon lists them: every settlement of the region whichever
    /// marketplace asks (North America pays USD, CAD, and MXN separately, MXN for the Mexican marketplace
    /// this app doesn't sell in as its own).
    /// </summary>
    private static readonly Dictionary<AmazonRegion, (string Code, string Currency, decimal PriceFactor, string OrderPrefix)[]> SettlementPayers = new()
    {
        [AmazonRegion.NorthAmerica] = [("US", "USD", 1.00m, "114"), ("CA", "CAD", 1.35m, "702"), ("MX", "MXN", 18.5m, "701")],
        [AmazonRegion.Europe] = [("UK", "GBP", 0.80m, "203")],
    };

    private readonly ConcurrentDictionary<string, Func<string>> _documents = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public Task<string> RequestReportAsync(Marketplace marketplace, AmazonReportType reportType, DateTimeOffset? dataStart, DateTimeOffset? dataEnd, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var reportId = $"SIM-{reportType}-{Guid.NewGuid():N}"[..32];
        var start = dataStart ?? clock.GetUtcNow().AddDays(-1);
        var end = dataEnd ?? clock.GetUtcNow();

        _documents[DocumentId(reportId)] = reportType switch
        {
            AmazonReportType.Orders => () => OrdersReport(marketplace, reportId, start, end),
            AmazonReportType.FbaInventory => () => InventoryReport(marketplace, reportId),
            AmazonReportType.FbaReservedInventory => () => ReservedInventoryReport(reportId),
            AmazonReportType.RestockRecommendations => () => RestockReport(marketplace, reportId),
            AmazonReportType.FbaInventoryPlanning => () => PlanningReport(marketplace, reportId),
            _ => throw new InvalidOperationException($"{reportType} reports cannot be requested; they are listed."),
        };

        return Task.FromResult(reportId);
    }

    /// <inheritdoc/>
    public Task<AmazonReportStatus> GetReportStatusAsync(Marketplace marketplace, string reportId, CancellationToken cancellationToken) =>
        Task.FromResult(_documents.ContainsKey(DocumentId(reportId))
            ? new AmazonReportStatus(AmazonProcessingStatus.Done, DocumentId(reportId))
            : new AmazonReportStatus(AmazonProcessingStatus.Fatal, null));

    /// <inheritdoc/>
    public Task<IReadOnlyList<AvailableAmazonReport>> ListCompletedReportsAsync(Marketplace marketplace, AmazonReportType reportType, DateTimeOffset createdSince, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        if (reportType != AmazonReportType.Settlements)
        {
            return Task.FromResult<IReadOnlyList<AvailableAmazonReport>>([]);
        }

        var now = clock.GetUtcNow();
        var reports = new List<AvailableAmazonReport>();
        for (var start = SettlementAnchor; start + SettlementPeriod <= now; start += SettlementPeriod)
        {
            var end = start + SettlementPeriod;
            var created = end.AddHours(6);
            if (created < createdSince || created > now)
            {
                continue;
            }

            // Like Amazon, every payout of the region is listed whichever marketplace asks; each currency
            // settles separately, so report (and settlement) ids carry the payer's code and are the same
            // for every marketplace of the region.
            foreach (var payer in SettlementPayers.GetValueOrDefault(marketplace.Region, []))
            {
                var reportId = string.Create(CultureInfo.InvariantCulture, $"SIM-SETTLE-{payer.Code}-{end:yyyyMMdd}");
                var periodStart = start;
                _documents[DocumentId(reportId)] = () => SettlementReport(payer.Currency, payer.PriceFactor, payer.OrderPrefix, reportId, periodStart, end);
                reports.Add(new AvailableAmazonReport(reportId, DocumentId(reportId), created));
            }
        }

        return Task.FromResult<IReadOnlyList<AvailableAmazonReport>>(reports);
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReportDocumentAsync(Marketplace marketplace, string reportDocumentId, CancellationToken cancellationToken) =>
        _documents.TryGetValue(reportDocumentId, out var build)
            ? Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(build())))
            : throw new InvalidOperationException($"Simulated document '{reportDocumentId}' does not exist.");

    private static string DocumentId(string reportId) => "DOC-" + reportId;

    /// <summary>Deterministic per report, so a re-download yields identical bytes (and SHA-256).</summary>
    private static Random Seeded(string reportId) => new(StringComparer.Ordinal.GetHashCode(reportId));

    private static (decimal PriceFactor, string OrderPrefix, double Volume) ProfileOf(Marketplace marketplace) =>
        Profiles.GetValueOrDefault(marketplace.MarketplaceId, (1.00m, "999", 0.5));

    private static string OrdersReport(Marketplace marketplace, string reportId, DateTimeOffset start, DateTimeOffset end)
    {
        var random = Seeded(reportId);
        var (priceFactor, prefix, volume) = ProfileOf(marketplace);
        var builder = new StringBuilder("amazon-order-id\tmerchant-order-id\tpurchase-date\tlast-updated-date\torder-status\tfulfillment-channel\tsales-channel\tproduct-name\tsku\tasin\titem-status\tquantity\tcurrency\titem-price\n");
        var span = end - start;
        var orderCount = Math.Clamp((int)(span.TotalHours / 3 * volume), 1, 400);

#pragma warning disable CA5394 // Simulated data; randomness is not security-sensitive.
        for (var i = 0; i < orderCount; i++)
        {
            var purchased = start + TimeSpan.FromSeconds(random.NextDouble() * span.TotalSeconds);
            var orderId = $"{prefix}-{random.Next(1_000_000, 9_999_999)}-{random.Next(1_000_000, 9_999_999)}";
            var status = random.Next(100) switch { < 85 => "Shipped", < 95 => "Pending", _ => "Cancelled" };
            foreach (var (sku, asin, title, price) in Catalog.OrderBy(_ => random.Next()).Take(random.Next(1, 3)))
            {
                var quantity = random.Next(1, 4);
                var linePrice = quantity * Math.Round(price * priceFactor, 2);
                builder.Append(CultureInfo.InvariantCulture,
                    $"{orderId}\t\t{purchased:yyyy-MM-ddTHH:mm:ss+00:00}\t{purchased:yyyy-MM-ddTHH:mm:ss+00:00}\t{status}\tAmazon\t{marketplace.SalesChannel}\t{title}\t{sku}\t{asin}\t{status}\t{quantity}\t{marketplace.Currency}\t{linePrice:0.00}\n");
            }
        }
#pragma warning restore CA5394

        return builder.ToString();
    }

    private static string InventoryReport(Marketplace marketplace, string reportId)
    {
        var random = Seeded(reportId);
        var (priceFactor, _, volume) = ProfileOf(marketplace);
        var builder = new StringBuilder("sku\tfnsku\tasin\tproduct-name\tcondition\tyour-price\tafn-fulfillable-quantity\tafn-unsellable-quantity\tafn-reserved-quantity\tafn-total-quantity\tafn-inbound-working-quantity\tafn-inbound-shipped-quantity\tafn-inbound-receiving-quantity\n");

#pragma warning disable CA5394 // Simulated data; randomness is not security-sensitive.
        foreach (var (sku, asin, title, price) in Catalog)
        {
            int fulfillable = (int)(random.Next(0, 400) * volume), unsellable = random.Next(0, 4), reserved = random.Next(0, 10);
            int working = random.Next(0, 3) == 0 ? random.Next(10, 100) : 0, shipped = random.Next(0, 60), receiving = random.Next(0, 20);
            builder.Append(CultureInfo.InvariantCulture,
                $"{sku}\tX00{asin[4..]}\t{asin}\t{title}\tNew\t{price * priceFactor:0.00}\t{fulfillable}\t{unsellable}\t{reserved}\t{fulfillable + unsellable + reserved}\t{working}\t{shipped}\t{receiving}\n");
        }
#pragma warning restore CA5394

        return builder.ToString();
    }

    private static string ReservedInventoryReport(string reportId)
    {
        var random = Seeded(reportId);
        // Amazon's current layout: reserved_staging in place of the documented reserved_fc-transfers.
        var builder = new StringBuilder("sku\tfnsku\tasin\tproduct-name\treserved_qty\treserved_customerorders\treserved_fc-processing\treserved_staging\tprogram\n");

#pragma warning disable CA5394 // Simulated data; randomness is not security-sensitive.
        foreach (var (sku, asin, title, _) in Catalog)
        {
            int customerOrders = random.Next(0, 8), staging = random.Next(0, 3) == 0 ? random.Next(5, 40) : 0, processing = random.Next(0, 4);
            builder.Append(CultureInfo.InvariantCulture,
                $"{sku}\tX00{asin[4..]}\t{asin}\t{title}\t{customerOrders + staging + processing}\t{customerOrders}\t{processing}\t{staging}\t\n");
        }
#pragma warning restore CA5394

        return builder.ToString();
    }

    private string RestockReport(Marketplace marketplace, string reportId)
    {
        var random = Seeded(reportId);
        var country = marketplace.Code;
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var builder = new StringBuilder("Country\tProduct Name\tFNSKU\tMerchant SKU\tASIN\tCondition\tRecommended replenishment qty\tRecommended ship date\tRecommended action\n");

#pragma warning disable CA5394 // Simulated data; randomness is not security-sensitive.
        foreach (var (sku, asin, title, _) in Catalog)
        {
            // Roughly half the catalog needs restocking, with ship dates a few days to two weeks out.
            var quantity = random.Next(0, 2) == 0 ? 0 : random.Next(10, 120);
            var shipDate = quantity > 0 ? today.AddDays(random.Next(2, 15)).ToString("MM/dd/yyyy", CultureInfo.InvariantCulture) : string.Empty;
            var action = quantity > 0 ? "Create shipping plan" : "No action required";
            builder.Append(CultureInfo.InvariantCulture,
                $"{country}\t{title}\tX00{asin[4..]}\t{sku}\t{asin}\tNew\t{quantity}\t{shipDate}\t{action}\n");
        }
#pragma warning restore CA5394

        return builder.ToString();
    }

    private string PlanningReport(Marketplace marketplace, string reportId)
    {
        var random = Seeded(reportId);
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var currency = marketplace.Currency;
        var builder = new StringBuilder(
            "snapshot-date\tsku\tfnsku\tasin\tproduct-name\tcondition\tavailable\tinv-age-0-to-90-days\tinv-age-91-to-180-days\tinv-age-181-to-270-days"
            + "\tinv-age-271-to-365-days\tinv-age-365-plus-days\tcurrency\testimated-excess-quantity\tdays-of-supply\testimated-storage-cost-next-month"
            + "\testimated-ais-181-210-days\testimated-ais-211-240-days\tmarketplace\n");

#pragma warning disable CA5394 // Simulated data; randomness is not security-sensitive.
        foreach (var (sku, asin, title, _) in Catalog)
        {
            // Mostly young stock; now and then a slow SKU with units past 180 days and some excess.
            var young = random.Next(20, 300);
            var middle = random.Next(0, 60);
            var aged = random.Next(0, 4) == 0 ? random.Next(5, 40) : 0;
            var excess = aged > 0 ? random.Next(0, aged + 1) : 0;
            var storage = Math.Round((young + middle + aged) * 0.05m, 2);
            var surcharge = Math.Round(aged * 0.5m, 2);
            builder.Append(CultureInfo.InvariantCulture,
                $"{today:yyyy-MM-dd}\t{sku}\tX00{asin[4..]}\t{asin}\t{title}\tNew\t{young + middle + aged}\t{young}\t{middle}\t{aged}\t0\t0\t{currency}\t{excess}\t{random.Next(20, 200)}\t{storage}\t{surcharge}\t0\t{marketplace.Code}\n");
        }
#pragma warning restore CA5394

        return builder.ToString();
    }

    private static string SettlementReport(string currency, decimal priceFactor, string prefix, string reportId, DateTimeOffset start, DateTimeOffset end)
    {
        var random = Seeded(reportId);
        var settlementId = reportId["SIM-SETTLE-".Length..];
        var builder = new StringBuilder("settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\ttransaction-type\torder-id\tamount-type\tamount-description\tamount\tposted-date\tsku\n");
        builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t{start:yyyy-MM-dd HH:mm:ss} UTC\t{end:yyyy-MM-dd HH:mm:ss} UTC\t{end.AddDays(1):yyyy-MM-dd HH:mm:ss} UTC\t\t{currency}\t\t\t\t\t\t\t\n");

#pragma warning disable CA5394 // Simulated data; randomness is not security-sensitive.
        for (var i = 0; i < 25; i++)
        {
            var (sku, _, _, basePrice) = Catalog[random.Next(Catalog.Length)];
            var price = Math.Round(basePrice * priceFactor, 2);
            var posted = start + TimeSpan.FromDays(random.NextDouble() * SettlementPeriod.TotalDays);
            var orderId = $"{prefix}-{random.Next(1_000_000, 9_999_999)}-{random.Next(1_000_000, 9_999_999)}";
            builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tOrder\t{orderId}\tItemPrice\tPrincipal\t{price:0.00}\t{posted:yyyy-MM-dd}\t{sku}\n");
            builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tOrder\t{orderId}\tItemFees\tCommission\t{-price * 0.15m:0.00}\t{posted:yyyy-MM-dd}\t{sku}\n");
            builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tOrder\t{orderId}\tItemFees\tFBAPerUnitFulfillmentFee\t{-(3 + random.NextDouble() * 3):0.00}\t{posted:yyyy-MM-dd}\t{sku}\n");

            // A few shipping chargebacks and refunds, so every settlement column has something in it.
            if (i % 5 == 0)
            {
                builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tOrder\t{orderId}\tItemFees\tShippingChargeback\t-2.49\t{posted:yyyy-MM-dd}\t{sku}\n");
            }

            if (i % 8 == 3)
            {
                var refunded = posted.AddDays(3);
                builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tRefund\t{orderId}\tItemPrice\tPrincipal\t{-price:0.00}\t{refunded:yyyy-MM-dd}\t{sku}\n");
                builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tRefund\t{orderId}\tItemFees\tCommission\t{price * 0.15m:0.00}\t{refunded:yyyy-MM-dd}\t{sku}\n");
                builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tRefund\t{orderId}\tItemFees\tRefundCommission\t{-Math.Min(5m, price * 0.03m):0.00}\t{refunded:yyyy-MM-dd}\t{sku}\n");
            }
        }

        // Amazon bills advertising as SKU-less lump sums, and inbound shipping per shipment.
        builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tServiceFee\t\tCost of Advertising\tTransactionTotalAmount\t{-(80 + random.NextDouble() * 120):0.00}\t{end.AddDays(-2):yyyy-MM-dd}\t\n");
        builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tother-transaction\t\tother-transaction\tInbound Transportation Fee\t{-(20 + random.NextDouble() * 30):0.00}\t{end.AddDays(-3):yyyy-MM-dd}\t\n");
#pragma warning restore CA5394

        builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tServiceFee\t\tother-transaction\tSubscription Fee\t-39.99\t{end.AddDays(-1):yyyy-MM-dd}\t\n");
        return builder.ToString();
    }
}
