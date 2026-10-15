using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Products;

/// <summary>Builds the downloadable cost-of-goods sheet for one marketplace.</summary>
public interface IProductCostTemplateService
{
    /// <summary>Creates the sheet with every product's current cost.</summary>
    /// <param name="marketplace">Marketplace whose costs to write (they are in its currency).</param>
    /// <param name="familyId">Only this family, or <see langword="null"/> for every product.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The file name and the .xlsx bytes.</returns>
    Task<(string FileName, byte[] Content)> CreateAsync(Marketplace marketplace, int? familyId, CancellationToken cancellationToken);
}
