using AERai.Web.Application.Bookkeeping;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>Read side of the bookkeeping export: settlement totals, account mappings, and generated files.</summary>
public interface IBookkeepingQueries
{
    /// <summary>Gets a settlement's totals per kind of amount.</summary>
    /// <param name="marketplaceId">Marketplace the settlement must belong to.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The settlement, or <see langword="null"/> when it does not exist in that marketplace.</returns>
    Task<SettlementBooks?> GetSettlementAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken);

    /// <summary>Lists every kind of settlement amount seen in any imported settlement, with how many lines have it.</summary>
    /// <remarks>
    /// Deliberately not marketplace-scoped: account mappings are shared by all marketplaces, so the mapping
    /// page must show every kind of amount any of them has produced.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>One entry per (amount type, amount description).</returns>
    Task<IReadOnlyList<SettlementCategoryTotal>> GetCategoriesAsync(CancellationToken cancellationToken);

    /// <summary>Gets every account mapping.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The mappings.</returns>
    Task<IReadOnlyList<BookkeepingAccountMapping>> GetMappingsAsync(CancellationToken cancellationToken);

    /// <summary>Gets a marketplace's deposit account name.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The account name, or <see langword="null"/> when not set.</returns>
    Task<string?> GetDepositAccountAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Lists a marketplace's generated files, newest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="maxCount">Most files to return.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The files, without their text.</returns>
    Task<IReadOnlyList<BookkeepingExportSummary>> GetExportsAsync(string marketplaceId, int maxCount, CancellationToken cancellationToken);

    /// <summary>Gets one generated file to download.</summary>
    /// <param name="marketplaceId">Marketplace the file must belong to.</param>
    /// <param name="exportId">Export identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The file, or <see langword="null"/> when it does not exist in that marketplace.</returns>
    Task<BookkeepingExportFile?> GetExportFileAsync(string marketplaceId, long exportId, CancellationToken cancellationToken);
}
