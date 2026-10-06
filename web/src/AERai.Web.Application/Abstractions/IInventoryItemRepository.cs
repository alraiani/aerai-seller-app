using AERai.Web.Application.Inventory;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>Persistence for the user-maintained parts of a SKU: family, picture, and home stock.</summary>
public interface IInventoryItemRepository
{
    /// <summary>A SKU's editable details in one marketplace.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="marketplaceId">Marketplace for home stock.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The details, or <see langword="null"/> when the SKU does not exist.</returns>
    Task<InventoryItemDetails?> GetAsync(string sku, string marketplaceId, CancellationToken cancellationToken);

    /// <summary>All families, by name.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The families.</returns>
    Task<IReadOnlyList<ProductFamily>> ListFamiliesAsync(CancellationToken cancellationToken);

    /// <summary>Finds a family by name (case-insensitively) or creates it.</summary>
    /// <param name="name">Trimmed, validated family name.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The family's id.</returns>
    Task<int> GetOrCreateFamilyAsync(string name, CancellationToken cancellationToken);

    /// <summary>Assigns a SKU to a family, or clears it.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="familyId">Family id, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="false"/> when the SKU does not exist.</returns>
    Task<bool> SetFamilyAsync(string sku, int? familyId, CancellationToken cancellationToken);

    /// <summary>Deletes families no product belongs to any more.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when unused families are gone.</returns>
    Task DeleteUnusedFamiliesAsync(CancellationToken cancellationToken);

    /// <summary>A SKU's picture location.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The path and MIME type, or <see langword="null"/> when there is no picture.</returns>
    Task<(string Path, string ContentType)?> GetImageAsync(string sku, CancellationToken cancellationToken);

    /// <summary>Points a SKU at a new picture, or clears it.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="path">Blob path, or <see langword="null"/> to clear.</param>
    /// <param name="contentType">MIME type, or <see langword="null"/> to clear.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns><see langword="false"/> when the SKU does not exist.</returns>
    Task<bool> SetImageAsync(string sku, string? path, string? contentType, CancellationToken cancellationToken);

    /// <summary>Which of the given SKUs exist in the product catalog.</summary>
    /// <param name="skus">SKUs to check.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The existing SKUs (ordinal comparison).</returns>
    Task<IReadOnlySet<string>> GetExistingSkusAsync(IReadOnlyCollection<string> skus, CancellationToken cancellationToken);

    /// <summary>Sets home stock for several SKUs in one marketplace; a quantity of 0 removes the row.</summary>
    /// <param name="marketplaceId">Marketplace the stock is held for.</param>
    /// <param name="entries">Validated entries for existing SKUs, one per SKU.</param>
    /// <param name="updatedAt">Change timestamp.</param>
    /// <param name="updatedBy">User email.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when all entries are saved (in one transaction).</returns>
    Task SetHomeStockAsync(string marketplaceId, IReadOnlyList<HomeStockEntry> entries, DateTimeOffset updatedAt, string updatedBy, CancellationToken cancellationToken);
}
