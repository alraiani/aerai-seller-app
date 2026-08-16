using AERai.Seller.Application.Abstractions;
using AERai.Seller.Domain;
using Microsoft.EntityFrameworkCore;

namespace AERai.Seller.Infrastructure.Repositories;

public sealed class BookkeepingSettingsRepository(IDbContextFactory<SellerDbContext> dbContextFactory) : IBookkeepingSettingsRepository
{
    private const int SingletonId = 1;

    public async Task<BookkeepingSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var settings = await dbContext.BookkeepingSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == SingletonId, cancellationToken);
        return settings ?? new BookkeepingSettings { Id = SingletonId };
    }

    public async Task SaveAsync(BookkeepingSettings settings, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var existing = await dbContext.BookkeepingSettings.FirstOrDefaultAsync(s => s.Id == SingletonId, cancellationToken);
        if (existing is null)
        {
            dbContext.BookkeepingSettings.Add(new BookkeepingSettings
            {
                Id = SingletonId,
                DepositAccountName = settings.DepositAccountName,
                ExportFolderPath = settings.ExportFolderPath,
            });
        }
        else
        {
            existing.DepositAccountName = settings.DepositAccountName;
            existing.ExportFolderPath = settings.ExportFolderPath;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
