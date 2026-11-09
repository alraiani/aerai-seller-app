using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>EF Core implementation of <see cref="IFinancialTransactionRepository"/>.</summary>
/// <param name="dbContext">Scoped database context.</param>
internal sealed class FinancialTransactionRepository(AppDbContext dbContext) : IFinancialTransactionRepository
{
    /// <inheritdoc/>
    public async Task<(int Inserted, int Updated)> UpsertAsync(
        string marketplaceId, IReadOnlyCollection<AmazonFinancialTransaction> transactions, DateTimeOffset syncedAt, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marketplaceId);
        ArgumentNullException.ThrowIfNull(transactions);
        if (transactions.Select(t => t.TransactionId).Distinct(StringComparer.Ordinal).Count() != transactions.Count)
        {
            throw new ArgumentException("Each transaction may appear only once.", nameof(transactions));
        }

        if (transactions.Count == 0)
        {
            return (0, 0);
        }

        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            var ids = transactions.Select(t => t.TransactionId).ToList();
            var existing = await dbContext.FinancialTransactions
                .Include(t => t.Lines)
                .Where(t => t.MarketplaceId == marketplaceId && ids.Contains(t.TransactionId))
                .ToDictionaryAsync(t => t.TransactionId, StringComparer.Ordinal, cancellationToken)
                .ConfigureAwait(false);

            var inserted = 0;
            foreach (var incoming in transactions)
            {
                if (!existing.TryGetValue(incoming.TransactionId, out var row))
                {
                    row = new FinancialTransaction { MarketplaceId = marketplaceId, TransactionId = Truncate(incoming.TransactionId, 128)!, Currency = incoming.Currency };
                    dbContext.FinancialTransactions.Add(row);
                    inserted++;
                }
                else
                {
                    // A re-read replaces the amounts wholesale: Amazon's latest breakdown is the one that counts.
                    dbContext.FinancialTransactionLines.RemoveRange(row.Lines);
                    row.Lines.Clear();
                }

                row.TransactionType = Truncate(incoming.TransactionType, 64);
                row.Description = Truncate(incoming.Description, 256);
                row.Status = Truncate(incoming.Status, 32);
                row.PostedDate = incoming.PostedDate;
                row.AmazonOrderId = Truncate(incoming.AmazonOrderId, 64);
                row.SettlementId = Truncate(incoming.SettlementId, 32);
                row.Amount = incoming.Amount;
                row.Currency = incoming.Currency.Length > 3 ? incoming.Currency[..3] : incoming.Currency;
                row.SyncedAt = syncedAt;
                row.Lines.AddRange(incoming.Lines.Select(l => new FinancialTransactionLine
                {
                    Sku = Truncate(l.Sku, 64),
                    Asin = Truncate(l.Asin, 16),
                    Units = l.Units,
                    BreakdownPath = Truncate(l.BreakdownPath, 400)!,
                    Amount = l.Amount,
                }));
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return (inserted, transactions.Count - inserted);
        }).ConfigureAwait(false);
    }

    /// <summary>Cuts Amazon's text to the column's length; it never sends longer, but one bad value mustn't fail a run.</summary>
    private static string? Truncate(string? value, int length) =>
        value is null ? null : value.Length <= length ? value : value[..length];
}
