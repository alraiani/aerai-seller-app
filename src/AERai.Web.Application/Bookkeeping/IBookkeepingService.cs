using AERai.Web.Application.Common;

namespace AERai.Web.Application.Bookkeeping;

/// <summary>Use cases for exporting settlements to QuickBooks Desktop.</summary>
public interface IBookkeepingService
{
    /// <summary>Shows how a settlement would be exported.</summary>
    /// <param name="marketplaceId">Marketplace the settlement must belong to.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The preview, or a failure when the settlement does not exist in that marketplace.</returns>
    Task<Result<SettlementPreview>> PreviewAsync(string marketplaceId, string settlementId, CancellationToken cancellationToken);

    /// <summary>Generates and stores the IIF file for a settlement.</summary>
    /// <param name="marketplaceId">Marketplace the settlement must belong to.</param>
    /// <param name="settlementId">Amazon settlement identifier.</param>
    /// <param name="requestedBy">Email of the user exporting.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The stored file, or a failure when the settlement is unknown, has unmapped amounts, or the marketplace has no deposit account.</returns>
    Task<Result<BookkeepingExportSummary>> ExportAsync(string marketplaceId, string settlementId, string requestedBy, CancellationToken cancellationToken);

    /// <summary>Lists the marketplace's generated files, newest first.</summary>
    /// <param name="marketplaceId">Marketplace.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The files.</returns>
    Task<IReadOnlyList<BookkeepingExportSummary>> GetExportsAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Gets a generated file to download.</summary>
    /// <param name="marketplaceId">Marketplace the file must belong to.</param>
    /// <param name="exportId">Export identifier.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The file, or <see langword="null"/> when it does not exist in that marketplace.</returns>
    Task<BookkeepingExportFile?> GetExportFileAsync(string marketplaceId, long exportId, CancellationToken cancellationToken);

    /// <summary>Gets what the mapping page edits.</summary>
    /// <param name="marketplaceId">Marketplace whose deposit account is shown.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every kind of amount seen with its account, and the deposit account.</returns>
    Task<AccountMappingView> GetMappingViewAsync(string marketplaceId, CancellationToken cancellationToken);

    /// <summary>Saves the accounts typed on the mapping page and the marketplace's deposit account.</summary>
    /// <param name="marketplaceId">Marketplace whose deposit account is saved.</param>
    /// <param name="depositAccountName">The deposit account; blank clears it.</param>
    /// <param name="mappings">The accounts typed; blank removes a mapping.</param>
    /// <param name="requestedBy">Email of the user saving.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure naming an account name that is too long or contains a tab or line break.</returns>
    Task<Result> SaveMappingsAsync(string marketplaceId, string? depositAccountName, IReadOnlyList<AccountMappingInput> mappings, string requestedBy, CancellationToken cancellationToken);
}
