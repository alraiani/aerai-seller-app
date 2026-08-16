using AERai.Seller.Domain.Staging;

namespace AERai.Seller.Application.Abstractions;

public interface ISettlementRepository
{
    /// <summary>Idempotent upsert keyed by SettlementId. Re-syncing a settlement already present
    /// replaces its line items wholesale (Amazon can amend a settlement's contents while it's still
    /// the most recent one), so this is safe to re-run.</summary>
    Task UpsertAsync(
        IEnumerable<SettlementReport> settlements,
        IEnumerable<SettlementLineItem> lineItems,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettlementReport>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SettlementLineItem>> GetLineItemsAsync(string settlementId, CancellationToken cancellationToken = default);

    /// <summary>Distinct (AmountType, AmountDescription) pairs seen across all synced line items — feeds the account mapping page.</summary>
    Task<IReadOnlyList<(string AmountType, string AmountDescription)>> GetDistinctCategoriesAsync(CancellationToken cancellationToken = default);
}
