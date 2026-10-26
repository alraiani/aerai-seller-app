using AERai.Web.Application.Ingestion;

namespace AERai.Web.Application.Abstractions;

/// <summary>Writes Sponsored Products spend to <c>core.AdSpend</c>.</summary>
public interface IAdSpendRepository
{
    /// <summary>
    /// Replaces a marketplace's ad spend for a range of days with the given rows, in one transaction.
    /// SKUs not yet in the product catalog are added to it.
    /// </summary>
    /// <param name="marketplaceId">Marketplace the advertising ran in.</param>
    /// <param name="start">First day replaced.</param>
    /// <param name="end">Last day replaced (inclusive).</param>
    /// <param name="rows">Every row Amazon reported for the range, one per (day, SKU, campaign); days left out had no spend.</param>
    /// <param name="defaultCurrency">Currency for rows that do not carry one (the marketplace's).</param>
    /// <param name="syncedAt">When the rows were pulled.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of rows written.</returns>
    Task<int> ReplaceRangeAsync(
        string marketplaceId, DateOnly start, DateOnly end, IReadOnlyCollection<AdSpendRow> rows, string defaultCurrency, DateTimeOffset syncedAt, CancellationToken cancellationToken);
}
