using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IPendingSendRepository"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class PendingSendRepository(AppDbContext dbContext) : IPendingSendRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<PendingSend>> ListOpenAsync(string marketplaceId, CancellationToken cancellationToken) =>
        await Project(Sends(marketplaceId).Where(m => m.ReconciledAt == null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PendingSend>> ListSinceAsync(string marketplaceId, IReadOnlyCollection<string> skus, DateTimeOffset since, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skus);

        var sends = new List<PendingSend>();

        // Chunked so a large catalog stays well under SQL Server's 2,100-parameter limit.
        foreach (var chunk in skus.Chunk(1000))
        {
            sends.AddRange(await Project(Sends(marketplaceId).Where(m => chunk.Contains(m.Sku) && m.CreatedAt >= since))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false));
        }

        return sends.OrderBy(s => s.CreatedAt).ThenBy(s => s.Id).ToList();
    }

    /// <inheritdoc/>
    public Task<PendingSend?> GetAsync(string marketplaceId, long id, CancellationToken cancellationToken) =>
        Project(Sends(marketplaceId).Where(m => m.Id == id)).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<int> ReconcileAsync(string marketplaceId, IReadOnlyList<PendingSendMatch> matches, DateTimeOffset at, string by, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matches);
        ArgumentException.ThrowIfNullOrWhiteSpace(by);

        var updated = 0;
        foreach (var match in matches)
        {
            // One statement per send; the ReconciledAt check keeps a send closed meanwhile as it is.
            updated += await dbContext.HomeStockMovements
                .Where(m => m.Id == match.Id && m.MarketplaceId == marketplaceId && m.Type == HomeStockMovementType.ShippedToAmazon && m.ReconciledAt == null)
                .ExecuteUpdateAsync(
                    u => u
                        .SetProperty(m => m.ReconciledUnits, match.ReconciledUnits)
                        .SetProperty(m => m.ReconciledAt, match.Closed ? at : null)
                        .SetProperty(m => m.ReconciledBy, match.Closed ? by : null),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return updated;
    }

    /// <inheritdoc/>
    public async Task<bool> SetReferenceAsync(string marketplaceId, long id, string reference, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);

        return await Sends(marketplaceId)
            .Where(m => m.Id == id && m.ReconciledAt == null && m.Reference == null)
            .ExecuteUpdateAsync(u => u.SetProperty(m => m.Reference, reference), cancellationToken)
            .ConfigureAwait(false) > 0;
    }

    /// <summary>A marketplace's sends to Amazon: Shipped to Amazon entries that took units out and weren't reversed.</summary>
    private IQueryable<HomeStockMovement> Sends(string marketplaceId) =>
        dbContext.HomeStockMovements
            .Where(m => m.MarketplaceId == marketplaceId && m.Type == HomeStockMovementType.ShippedToAmazon && m.Units < 0)
            .Where(m => !dbContext.HomeStockMovements.Any(r => r.ReversesId == m.Id));

    private IQueryable<PendingSend> Project(IQueryable<HomeStockMovement> sends) =>
        from m in sends.AsNoTracking()
        join p in dbContext.Products on m.Sku equals p.Sku
        orderby m.CreatedAt, m.Id
        select new PendingSend(
            m.Id,
            m.Sku,
            p.Title,
            p.FamilyId,
            p.Color,
            m.OccurredAt,
            m.CreatedAt,
            m.CreatedBy,
            -m.Units,
            m.ReconciledUnits,
            m.AmazonBaselineUnits == null ? null : new AmazonBaseline(m.AmazonBaselineUnits.Value, m.AmazonBaselineAt),
            m.Reference,
            m.ReconciledAt == null);
}
