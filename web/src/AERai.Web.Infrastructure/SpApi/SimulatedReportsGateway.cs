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

    private static readonly (string Sku, string Asin, string Title, decimal Price)[] Catalog =
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

            // Each marketplace settles separately, so report (and settlement) ids carry its code.
            var reportId = string.Create(CultureInfo.InvariantCulture, $"SIM-SETTLE-{marketplace.Code}-{end:yyyyMMdd}");
            var periodStart = start;
            _documents[DocumentId(reportId)] = () => SettlementReport(marketplace, reportId, periodStart, end);
            reports.Add(new AvailableAmazonReport(reportId, DocumentId(reportId), created));
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
        var builder = new StringBuilder("sku\tfnsku\tasin\tproduct-name\treserved_qty\treserved_customerorders\treserved_fc-transfers\treserved_fc-processing\n");

#pragma warning disable CA5394 // Simulated data; randomness is not security-sensitive.
        foreach (var (sku, asin, title, _) in Catalog)
        {
            int customerOrders = random.Next(0, 8), transfers = random.Next(0, 3) == 0 ? random.Next(5, 40) : 0, processing = random.Next(0, 4);
            builder.Append(CultureInfo.InvariantCulture,
                $"{sku}\tX00{asin[4..]}\t{asin}\t{title}\t{customerOrders + transfers + processing}\t{customerOrders}\t{transfers}\t{processing}\n");
        }
#pragma warning restore CA5394

        return builder.ToString();
    }

    private static string SettlementReport(Marketplace marketplace, string reportId, DateTimeOffset start, DateTimeOffset end)
    {
        var random = Seeded(reportId);
        var (priceFactor, prefix, _) = ProfileOf(marketplace);
        var currency = marketplace.Currency;
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
        }
#pragma warning restore CA5394

        builder.Append(CultureInfo.InvariantCulture, $"{settlementId}\t\t\t\t\t\tServiceFee\t\tother-transaction\tSubscription Fee\t-39.99\t{end.AddDays(-1):yyyy-MM-dd}\t\n");
        return builder.ToString();
    }
}
