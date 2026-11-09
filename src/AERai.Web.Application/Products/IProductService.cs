using AERai.Web.Application.Common;

namespace AERai.Web.Application.Products;

/// <summary>
/// Product master-data use cases.
/// </summary>
public interface IProductService
{
    /// <summary>Sets or clears a product's unit cost of goods in one marketplace, in its currency.</summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="marketplaceId">Marketplace the cost applies to.</param>
    /// <param name="costOfGoods">Unit cost (0 – <see cref="ProductService.MaxCostOfGoods"/>), or <see langword="null"/> to clear.</param>
    /// <param name="changedBy">User email recorded in the cost history.</param>
    /// <returns>Success, or a failure when the value is out of range or the SKU is unknown.</returns>
    /// <param name="cancellationToken">Cancels the operation.</param>
    Task<Result> UpdateCostAsync(string sku, string marketplaceId, decimal? costOfGoods, string changedBy, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a SKU's cost changes in one marketplace, newest first (at most <see cref="ProductService.MaxCostHistory"/>).
    /// Profit charges each sale the cost in effect on its order date, so this explains past figures.
    /// </summary>
    /// <param name="sku">Seller SKU.</param>
    /// <param name="marketplaceId">Marketplace whose costs to list.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The changes.</returns>
    Task<IReadOnlyList<ProductCostHistoryEntry>> GetCostHistoryAsync(string sku, string marketplaceId, CancellationToken cancellationToken);

    /// <summary>
    /// Saves several cost changes at once (the Products page's inline edits, or a reviewed cost
    /// sheet). Costs are rounded to cents; a SKU listed twice keeps its last change; changes that
    /// change nothing are dropped. A SKU whose cost changed since its <see cref="ProductCostChange.Current"/>
    /// was read is skipped and reported instead of overwritten.
    /// </summary>
    /// <param name="marketplaceId">Marketplace the costs apply to.</param>
    /// <param name="changes">The changes; a <see langword="null"/> new cost clears it.</param>
    /// <param name="changedBy">User email recorded in the cost history.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>How many were saved and which were stale, or a failure (nothing saved) when a cost is out of range.</returns>
    Task<Result<ProductCostResult>> SaveCostsAsync(string marketplaceId, IReadOnlyList<ProductCostChange> changes, string changedBy, CancellationToken cancellationToken);

    /// <summary>
    /// Checks an uploaded cost sheet (columns <c>sku</c> and <c>cost-of-goods</c>; .xlsx, .csv, or
    /// .tsv) against current costs without saving anything. A blank cost leaves the SKU's cost as it
    /// is; a SKU listed twice keeps its last row.
    /// </summary>
    /// <param name="marketplaceId">Marketplace the costs apply to.</param>
    /// <param name="fileName">The uploaded file's name (chooses the parser).</param>
    /// <param name="content">The file; read, not disposed.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The changes, unchanged and blank counts, and rejected rows; or a failure when the file can't be read or lacks a column.</returns>
    Task<Result<ProductCostReconciliation>> PreviewCostImportAsync(string marketplaceId, string fileName, Stream content, CancellationToken cancellationToken);
}
