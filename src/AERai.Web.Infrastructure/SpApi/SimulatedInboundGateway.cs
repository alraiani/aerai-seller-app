using System.Globalization;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Inbound shipments gateway for <see cref="SpApiMode.Simulated"/>: one Send to Amazon plan a week for the last few
/// weeks, each with one shipment of two or three SKUs from the catalog's families that ship from home stock (the
/// simulated product range when there are none). This week's shipment is still being worked on and
/// older ones have shipped, so a local run has something to review. The JSON has Amazon's shape and is parsed by
/// <see cref="InboundApiClient"/>, like a live response. Nothing leaves the machine.
/// </summary>
/// <remarks>
/// Ids and quantities derive from the marketplace and the week, so repeated runs return the same shipments and a new
/// one appears each week. Only the catalog is read; nothing is written.
/// </remarks>
/// <param name="time">Clock (which weeks have plans).</param>
/// <param name="dbContext">Database, for the SKUs that ship from home stock; <see langword="null"/> uses the simulated range.</param>
internal sealed class SimulatedInboundGateway(TimeProvider time, AppDbContext? dbContext = null) : IAmazonInboundGateway
{
    /// <summary>Most home-stock SKUs the shipments are drawn from.</summary>
    private const int MaxSkus = 12;

    /// <summary>How many weekly plans exist, the newest first.</summary>
    internal const int Weeks = 3;

    /// <summary>Plans per simulated page; small so a run spans several pages.</summary>
    internal const int PageSize = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public Task<InboundPlanPage> ListPlansPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var start = nextToken is null ? 0 : int.Parse(nextToken, NumberStyles.None, CultureInfo.InvariantCulture);
        var plans = Enumerable.Range(0, Weeks).Select(age => Plan(marketplace, age)).ToList();
        var page = plans.Skip(start).Take(PageSize).ToList();
        var next = start + PageSize < plans.Count ? (start + PageSize).ToString(CultureInfo.InvariantCulture) : null;
        var body = new
        {
            inboundPlans = page.Select(p => new
            {
                inboundPlanId = p.PlanId,
                name = p.Name,
                status = p.Age == 0 ? "ACTIVE" : "SHIPPED",
                createdAt = p.CreatedAt,
                lastUpdatedAt = p.CreatedAt.AddHours(6),
                marketplaceIds = new[] { marketplace.MarketplaceId },
            }),
            pagination = new { nextToken = next },
        };
        return Task.FromResult(InboundApiClient.ParsePlansPage(JsonSerializer.Serialize(body, JsonOptions)));
    }

    /// <inheritdoc/>
    public Task<InboundPlanShipments> GetPlanShipmentsAsync(Marketplace marketplace, string inboundPlanId, CancellationToken cancellationToken)
    {
        var plan = Find(marketplace, inboundPlanId);
        var body = new { inboundPlanId, shipments = new[] { new { shipmentId = plan.ShipmentId, status = Status(plan) } } };
        return Task.FromResult(InboundApiClient.ParsePlan(JsonSerializer.Serialize(body, JsonOptions), inboundPlanId));
    }

    /// <inheritdoc/>
    public Task<InboundShipmentDetail> GetShipmentAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, CancellationToken cancellationToken)
    {
        var plan = Find(marketplace, inboundPlanId);
        var body = new { shipmentId, shipmentConfirmationId = plan.ConfirmationId, name = plan.Name, status = Status(plan) };
        return Task.FromResult(InboundApiClient.ParseShipment(JsonSerializer.Serialize(body, JsonOptions), shipmentId));
    }

    /// <inheritdoc/>
    public async Task<InboundShipmentItemsPage> ListShipmentItemsPageAsync(Marketplace marketplace, string inboundPlanId, string shipmentId, string? nextToken, CancellationToken cancellationToken)
    {
        var plan = Find(marketplace, inboundPlanId);
        var skus = await SkusAsync(cancellationToken).ConfigureAwait(false);
        var hash = Hash(plan.PlanId);
        var count = 2 + (int)(hash % 2);
        var items = Enumerable.Range(0, count)
            .Select(i => skus[(int)((hash + (uint)i) % (uint)skus.Count)])
            .Distinct(StringComparer.Ordinal)
            .Select(sku => new { msku = sku, quantity = 5 * (2 + (int)(Hash(sku + plan.PlanId) % 8)) });
        var body = new { items, pagination = new { nextToken = (string?)null } };
        return InboundApiClient.ParseItemsPage(JsonSerializer.Serialize(body, JsonOptions));
    }

    /// <summary>The SKUs shipments are made of: home-stock families' first SKUs, else the simulated range.</summary>
    private async Task<IReadOnlyList<string>> SkusAsync(CancellationToken cancellationToken)
    {
        var home = dbContext is null
            ? []
            : await dbContext.Products.AsNoTracking()
                .Where(p => dbContext.ProductFamilies.Any(f => f.Id == p.FamilyId && f.ShipsFromHomeStock))
                .OrderBy(p => p.Sku)
                .Select(p => p.Sku)
                .Take(MaxSkus)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        return home.Count > 0 ? home : SimulatedReportsGateway.Catalog.Select(p => p.Sku).ToList();
    }

    private static string Status(SimulatedPlan plan) => plan.Age switch
    {
        0 => "WORKING",
        1 => "IN_TRANSIT",
        _ => "RECEIVING",
    };

    private SimulatedPlan Find(Marketplace marketplace, string inboundPlanId)
    {
        ArgumentNullException.ThrowIfNull(marketplace);
        return Enumerable.Range(0, Weeks).Select(age => Plan(marketplace, age)).FirstOrDefault(p => p.PlanId == inboundPlanId)
            ?? throw new InvalidOperationException($"Simulated inbound plan {inboundPlanId} does not exist.");
    }

    private SimulatedPlan Plan(Marketplace marketplace, int age)
    {
        // Each plan is created on a Monday, so the same week always yields the same ids.
        var today = time.GetUtcNow().UtcDateTime.Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).AddDays(-7 * age);
        var week = string.Create(CultureInfo.InvariantCulture, $"{ISOWeek.GetYear(monday) % 100:00}{ISOWeek.GetWeekOfYear(monday):00}");
        var code = marketplace.Code.ToUpperInvariant();
        return new SimulatedPlan(
            age,
            $"wfSIM{code}{week}",
            $"shSIM{code}{week}",
            $"FBA{code}SIM{week}",
            string.Create(CultureInfo.InvariantCulture, $"Simulated {code} send {monday:MMM d}"),
            new DateTimeOffset(monday.AddHours(15), TimeSpan.Zero));
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

    private sealed record SimulatedPlan(int Age, string PlanId, string ShipmentId, string ConfirmationId, string Name, DateTimeOffset CreatedAt);
}
