using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class BookkeepingExportRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IBookkeepingExportRepository
{
    public async Task InsertAsync(BookkeepingExportRecord record, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        dbContext.BookkeepingExportRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookkeepingExportRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.BookkeepingExportRecords.AsNoTracking().ToListAsync(cancellationToken);
    }
}
