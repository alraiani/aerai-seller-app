using AERai.Web.Application.Bookkeeping;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>Write side of the bookkeeping export.</summary>
public interface IBookkeepingStore
{
    /// <summary>
    /// Sets the account for each given kind of amount, in one transaction: a blank account removes the
    /// mapping, and kinds not listed are left alone.
    /// </summary>
    /// <param name="mappings">The accounts to set.</param>
    /// <param name="updatedAt">When the change was made.</param>
    /// <param name="updatedBy">Email of the user making it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the mappings are saved.</returns>
    Task SaveMappingsAsync(IReadOnlyCollection<AccountMappingInput> mappings, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken);

    /// <summary>Sets, or with a blank name clears, a marketplace's deposit account.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="depositAccountName">The account name, or blank to clear.</param>
    /// <param name="updatedAt">When the change was made.</param>
    /// <param name="updatedBy">Email of the user making it.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the setting is saved.</returns>
    Task SaveDepositAccountAsync(string marketplaceId, string? depositAccountName, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken);

    /// <summary>Records a generated file.</summary>
    /// <param name="export">The export to store.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The identifier the file was given.</returns>
    Task<long> AddExportAsync(BookkeepingExport export, CancellationToken cancellationToken);
}
