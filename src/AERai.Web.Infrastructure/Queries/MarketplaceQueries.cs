using AERai.Web.Application.Abstractions;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Queries;

/// <summary>EF Core implementation of <see cref="IMarketplaceQueries"/> over <c>core.Marketplace</c>.</summary>
/// <param name="dbContext">Database context.</param>
internal sealed class MarketplaceQueries(AppDbContext dbContext) : IMarketplaceQueries
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<Marketplace>> GetAllAsync(CancellationToken cancellationToken) =>
        await dbContext.Marketplaces
            .AsNoTracking()
            .OrderBy(m => m.SortOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
}
