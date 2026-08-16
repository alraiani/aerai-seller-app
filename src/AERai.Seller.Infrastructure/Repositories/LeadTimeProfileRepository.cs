using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain.Ai;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class LeadTimeProfileRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : ILeadTimeProfileRepository
{
    public async Task UpsertAsync(LeadTimeProfile profile, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.LeadTimeProfiles.FindAsync([profile.Sku], cancellationToken);
        if (existing is null)
        {
            dbContext.LeadTimeProfiles.Add(profile);
        }
        else
        {
            existing.SupplierLeadTimeDays = profile.SupplierLeadTimeDays;
            existing.PrepTimeDays = profile.PrepTimeDays;
            existing.FbaTransitDays = profile.FbaTransitDays;
            existing.SafetyStockDays = profile.SafetyStockDays;
            existing.TargetStockDays = profile.TargetStockDays;
            existing.UpdatedAt = profile.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<LeadTimeProfile?> GetAsync(string sku, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.LeadTimeProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Sku == sku, cancellationToken);
    }

    public async Task<IReadOnlyList<LeadTimeProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.LeadTimeProfiles.AsNoTracking().ToListAsync(cancellationToken);
    }
}
