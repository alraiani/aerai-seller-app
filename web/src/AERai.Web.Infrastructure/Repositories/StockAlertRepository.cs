using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Alerts;
using AERai.Web.Domain.Alerts;
using AERai.Web.Infrastructure.Persistence;
using AERai.Web.Infrastructure.Persistence.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IStockAlertRepository"/>.
/// </summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class StockAlertRepository(AppDbContext dbContext) : IStockAlertRepository
{
    /// <inheritdoc/>
    public async Task<IReadOnlyList<StockAlert>> GetOpenAsync(string marketplaceId, CancellationToken cancellationToken) =>
        await dbContext.StockAlerts.AsNoTracking()
            .Where(a => a.MarketplaceId == marketplaceId && a.ResolvedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<bool> ApplyAsync(StockAlertChanges changes, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // Resolve first, so a SKU whose level changed can get its new open alert in the same save.
            // The ResolvedAt check makes a concurrent resolve by another instance a no-op here.
            if (changes.Resolve.Count > 0)
            {
                await dbContext.StockAlerts
                    .Where(a => changes.Resolve.Contains(a.Id) && a.ResolvedAt == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.ResolvedAt, now), cancellationToken)
                    .ConfigureAwait(false);
            }

            foreach (var (id, message) in changes.Messages)
            {
                await dbContext.StockAlerts
                    .Where(a => a.Id == id && a.ResolvedAt == null)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.Message, message).SetProperty(a => a.UpdatedAt, now), cancellationToken)
                    .ConfigureAwait(false);
            }

            dbContext.StockAlerts.AddRange(changes.Raise);
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 } sql
                && sql.Message.Contains(StockAlertConfiguration.OneOpenAlertIndex, StringComparison.Ordinal))
            {
                // Another instance opened the same alert first; drop this refresh and let the next one converge.
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                dbContext.ChangeTracker.Clear();
                return false;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<int> CountUnreadAsync(string marketplaceId, string userEmail, CancellationToken cancellationToken) =>
        dbContext.StockAlerts
            .Where(a => a.MarketplaceId == marketplaceId && a.ResolvedAt == null)
            .Where(a => !dbContext.StockAlertReads.Any(r => r.StockAlertId == a.Id && r.UserEmail == userEmail))
            .CountAsync(cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<StockAlertView>> ListAsync(string marketplaceId, string userEmail, DateTimeOffset resolvedSince, CancellationToken cancellationToken) =>
        await (from a in dbContext.StockAlerts.AsNoTracking()
               where a.MarketplaceId == marketplaceId && (a.ResolvedAt == null || a.ResolvedAt >= resolvedSince)
               join p in dbContext.Products on a.Sku equals p.Sku into products
               from p in products.DefaultIfEmpty()
               select new StockAlertView(
                   a.Id,
                   a.Sku,
                   p == null ? null : p.Title,
                   a.Level,
                   a.Message,
                   a.RaisedAt,
                   a.ResolvedAt,
                   dbContext.StockAlertReads.Any(r => r.StockAlertId == a.Id && r.UserEmail == userEmail)))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    /// <inheritdoc/>
    public async Task<int> MarkReadAsync(string marketplaceId, IReadOnlyCollection<long>? alertIds, string userEmail, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var unread = dbContext.StockAlerts
            .Where(a => a.MarketplaceId == marketplaceId)
            .Where(a => !dbContext.StockAlertReads.Any(r => r.StockAlertId == a.Id && r.UserEmail == userEmail));
        unread = alertIds is null ? unread.Where(a => a.ResolvedAt == null) : unread.Where(a => alertIds.Contains(a.Id));

        var ids = await unread.Select(a => a.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        dbContext.StockAlertReads.AddRange(ids.Select(id => new StockAlertRead { StockAlertId = id, UserEmail = userEmail, ReadAt = now }));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2627 })
        {
            // The same user marked them read in another tab at the same moment; already done.
            dbContext.ChangeTracker.Clear();
        }

        return ids.Count;
    }
}
