using AERai.Web.Domain.Core;
using AERai.Web.Infrastructure.Persistence;

namespace AERai.Web.Infrastructure.Repositories;

/// <summary>
/// Keeps <c>core.HomeStock</c> (the balance) in step with <c>core.HomeStockMovement</c> (the ledger).
/// Every home-stock write in the app goes through here, inside the caller's transaction.
/// </summary>
internal static class HomeStockBalance
{
    /// <summary>
    /// Adds a ledger entry for a change in a SKU's balance and updates (or adds, or removes) its
    /// balance row. Changes are tracked, not saved; the caller saves within its transaction.
    /// </summary>
    /// <param name="db">Context with the caller's transaction.</param>
    /// <param name="row">The SKU's current balance row, or <see langword="null"/> when it has none (0).</param>
    /// <param name="write">The entry; <see cref="Application.Inventory.HomeStockLedgerWrite.Units"/> is the signed change.</param>
    /// <returns>The tracked entry (its id is set on save).</returns>
    public static HomeStockMovement Apply(AppDbContext db, HomeStock? row, Application.Inventory.HomeStockLedgerWrite write)
    {
        var balance = (row?.Quantity ?? 0) + write.Units;
        var movement = new HomeStockMovement
        {
            MarketplaceId = write.MarketplaceId,
            Sku = write.Sku,
            OccurredAt = write.OccurredAt,
            Type = write.Type,
            Units = write.Units,
            BalanceAfter = balance,
            Reference = write.Reference,
            Note = write.Note,
            ReversesId = write.ReversesId,
            CreatedAt = write.CreatedAt,
            CreatedBy = write.CreatedBy,
        };
        db.HomeStockMovements.Add(movement);

        // Zero is stored as "no row", the same way an unset cost of goods is.
        if (balance == 0)
        {
            if (row is not null)
            {
                db.HomeStocks.Remove(row);
            }
        }
        else if (row is null)
        {
            db.HomeStocks.Add(new HomeStock { Sku = write.Sku, MarketplaceId = write.MarketplaceId, Quantity = balance, UpdatedAt = write.CreatedAt, UpdatedBy = write.CreatedBy });
        }
        else
        {
            row.Quantity = balance;
            row.UpdatedAt = write.CreatedAt;
            row.UpdatedBy = write.CreatedBy;
        }

        return movement;
    }
}
