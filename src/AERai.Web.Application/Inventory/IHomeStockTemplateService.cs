using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>Builds the downloadable home-stock spreadsheet that maps straight onto the upload.</summary>
public interface IHomeStockTemplateService
{
    /// <summary>The template for a marketplace, optionally just one family.</summary>
    /// <param name="marketplace">Marketplace the counts are for.</param>
    /// <param name="familyId">Only this family's SKUs, or <see langword="null"/> for every SKU.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A file name and the .xlsx bytes.</returns>
    Task<(string FileName, byte[] Content)> CreateAsync(Marketplace marketplace, int? familyId, CancellationToken cancellationToken);
}
