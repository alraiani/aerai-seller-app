using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class BookkeepingAccountMappingRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IBookkeepingAccountMappingRepository
{
    public async Task<IReadOnlyList<BookkeepingAccountMapping>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.BookkeepingAccountMappings.AsNoTracking().ToListAsync(cancellationToken);
    }

    public async Task UpsertAsync(BookkeepingAccountMapping mapping, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.BookkeepingAccountMappings.FirstOrDefaultAsync(
            m => m.AmountType == mapping.AmountType && m.AmountDescription == mapping.AmountDescription, cancellationToken);

        if (existing is null)
        {
            dbContext.BookkeepingAccountMappings.Add(mapping);
        }
        else
        {
            existing.QuickBooksAccountName = mapping.QuickBooksAccountName;
            existing.UpdatedAt = mapping.UpdatedAt;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
