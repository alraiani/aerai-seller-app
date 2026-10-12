using System.Globalization;
using System.Text.Json;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// AWD gateway for <see cref="SpApiMode.Simulated"/>: two in three of the simulated SKUs have AWD stock,
/// returned two per page so pagination is exercised locally. The JSON has Amazon's shape and is
/// parsed by <see cref="AwdApiClient.ParsePage"/>, like a live response. Nothing leaves the machine.
/// </summary>
internal sealed class SimulatedAwdGateway : IAmazonAwdGateway
{
    /// <summary>SKUs per simulated page; small so a run spans several pages.</summary>
    internal const int PageSize = 2;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public Task<AwdInventoryPage> ListInventoryPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var start = nextToken is null ? 0 : int.Parse(nextToken, NumberStyles.None, CultureInfo.InvariantCulture);
        var all = SimulatedReportsGateway.Catalog
            .Where((_, index) => index % 3 != 2)
            .Select(p => p.Sku)
            .ToList();
        var page = all.Skip(start).Take(PageSize).ToList();
        var next = start + PageSize < all.Count ? (start + PageSize).ToString(CultureInfo.InvariantCulture) : null;

        // Quantities derive from the SKU, so repeated runs give the same numbers.
        var body = new
        {
            inventory = page.Select(sku =>
            {
                var h = Hash(sku);
                int available = 40 + (int)(h % 300), reserved = (int)(h % 25), replenishment = h % 3 == 0 ? 24 + (int)(h % 60) : 0;
                return new
                {
                    sku,
                    totalOnhandQuantity = available + reserved,
                    totalInboundQuantity = h % 4 == 0 ? 120 + (int)(h % 200) : 0,
                    inventoryDetails = new
                    {
                        availableDistributableQuantity = available,
                        reservedDistributableQuantity = reserved,
                        replenishmentQuantity = replenishment,
                    },
                };
            }),
            nextToken = next,
        };

        return Task.FromResult(AwdApiClient.ParsePage(JsonSerializer.Serialize(body, JsonOptions)));
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
