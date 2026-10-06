using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>Use cases for editing a SKU's family, picture, and home stock.</summary>
public interface IInventoryItemService
{
    /// <summary>A SKU's editable details.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="marketplaceId">Marketplace for home stock.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The details, or <see langword="null"/> when the SKU does not exist.</returns>
    Task<InventoryItemDetails?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken);

    /// <summary>All families, by name.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The families.</returns>
    Task<IReadOnlyList<ProductFamily>> ListFamiliesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves a SKU's family (created on first use; blank clears it), its home stock, and its
    /// lead-time overrides in a marketplace.
    /// </summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="marketplaceId">Marketplace for home stock and lead times.</param>
    /// <param name="update">The submitted values.</param>
    /// <param name="user">Email of the user making the change.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a validation failure (nothing is saved then).</returns>
    Task<Result> UpdateAsync(string sku, string marketplaceId, InventoryItemUpdate update, string user, CancellationToken cancellationToken);

    /// <summary>Validates and stores a new picture for a SKU, replacing any previous one.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="content">Uploaded bytes.</param>
    /// <param name="length">Declared upload size in bytes.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why the picture was not accepted.</returns>
    Task<Result> SetImageAsync(string sku, Stream content, long length, CancellationToken cancellationToken);

    /// <summary>Removes a SKU's picture.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or "not found".</returns>
    Task<Result> RemoveImageAsync(string sku, CancellationToken cancellationToken);

    /// <summary>Opens a SKU's picture for serving.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The picture, or <see langword="null"/> when there is none.</returns>
    Task<ProductImage?> OpenImageAsync(string sku, CancellationToken cancellationToken);

    /// <summary>Saves typed-in home-stock counts for several SKUs at once.</summary>
    /// <param name="marketplaceId">Marketplace the stock is held for.</param>
    /// <param name="entries">The counts; SKUs must exist and quantities be in range.</param>
    /// <param name="user">Email of the user making the change.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number saved, or a failure naming the first invalid entry (nothing is saved then).</returns>
    Task<Result<int>> SetHomeStockAsync(string marketplaceId, IReadOnlyList<HomeStockEntry> entries, string user, CancellationToken cancellationToken);

    /// <summary>
    /// Imports home stock from a .csv, .tsv, or .xlsx file with columns <c>sku</c> and <c>home-stock</c>.
    /// Valid rows are saved; invalid rows are reported and skipped.
    /// </summary>
    /// <param name="marketplaceId">Marketplace the stock is held for.</param>
    /// <param name="fileName">Original file name (its extension picks the format).</param>
    /// <param name="content">File content.</param>
    /// <param name="user">Email of the user making the change.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>What was saved and rejected, or a failure when the file cannot be read at all.</returns>
    Task<Result<HomeStockImportResult>> ImportHomeStockAsync(string marketplaceId, string fileName, Stream content, string user, CancellationToken cancellationToken);
}
