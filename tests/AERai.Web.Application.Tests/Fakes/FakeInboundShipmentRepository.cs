using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests.Fakes;

/// <summary>In-memory <see cref="IInboundShipmentRepository"/>.</summary>
internal sealed class FakeInboundShipmentRepository : IInboundShipmentRepository
{
    public List<(string MarketplaceId, IReadOnlyCollection<AmazonInboundShipment> Shipments)> Applied { get; } = [];

    public List<InboundShipment> Shipments { get; } = [];

    /// <summary>Catalog facts per SKU.</summary>
    public Dictionary<string, InboundSkuInfo> Skus { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Units logged per reference and SKU; defaults to nothing.</summary>
    public Func<string, IReadOnlyDictionary<string, int>?> Logged { get; set; } = _ => null;

    public Task<InboundApplyResult> ApplyAsync(string marketplaceId, IReadOnlyCollection<AmazonInboundShipment> shipments, DateTimeOffset seenAt, CancellationToken cancellationToken)
    {
        Applied.Add((marketplaceId, shipments));
        return Task.FromResult(new InboundApplyResult(shipments.Count, shipments.Count, shipments.Count, 0, 0, 0));
    }

    public Task<IReadOnlyList<InboundShipment>> ListAsync(string marketplaceId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<InboundShipment>>(Shipments.Where(s => s.MarketplaceId == marketplaceId).Take(limit).ToList());

    public Task<InboundShipment?> GetAsync(string marketplaceId, long id, CancellationToken cancellationToken) =>
        Task.FromResult(Shipments.FirstOrDefault(s => s.MarketplaceId == marketplaceId && s.Id == id));

    public Task<bool> SetReviewAsync(string marketplaceId, long id, InboundShipmentReview expected, InboundShipmentReview review, DateTimeOffset at, string by, CancellationToken cancellationToken)
    {
        var shipment = Shipments.FirstOrDefault(s => s.MarketplaceId == marketplaceId && s.Id == id && s.Review == expected);
        if (shipment is null)
        {
            return Task.FromResult(false);
        }

        (shipment.Review, shipment.ReviewedAt, shipment.ReviewedBy) = (review, at, by);
        return Task.FromResult(true);
    }

    public Task<int> CountToReviewAsync(string marketplaceId, CancellationToken cancellationToken) =>
        Task.FromResult(Shipments.Count(s => s.MarketplaceId == marketplaceId && s.Review == InboundShipmentReview.ToReview
            && s.Items.Any(i => Skus.GetValueOrDefault(i.Sku)?.ShipsFromHomeStock == true)));

    public Task<IReadOnlyDictionary<string, InboundSkuInfo>> GetSkuInfoAsync(string marketplaceId, IReadOnlyCollection<string> skus, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, InboundSkuInfo>>(skus
            .Where(Skus.ContainsKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(s => s, s => Skus[s], StringComparer.OrdinalIgnoreCase));

    public Task<IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>> GetLoggedUnitsAsync(string marketplaceId, IReadOnlyCollection<string> references, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>>(references
            .Select(r => (Reference: r, Units: Logged(r)))
            .Where(r => r.Units is { Count: > 0 })
            .ToDictionary(r => r.Reference, r => r.Units!, StringComparer.OrdinalIgnoreCase)); // Non-null: filtered just above.
}
