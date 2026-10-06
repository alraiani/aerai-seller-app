using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Marketplaces;
using AERai.Web.Application.Products;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;

namespace AERai.Web.UI.Pages.Products;

/// <summary>
/// Product master data and cost of goods.
/// </summary>
/// <param name="products">Product queries.</param>
/// <param name="currentMarketplace">The marketplace the user is viewing.</param>
public sealed class IndexModel(IProductRepository products, ICurrentMarketplace currentMarketplace) : ListPageModel
{
    /// <summary>The current page of products.</summary>
    public PagedResult<ProductSummary> Products { get; private set; } = default!;

    /// <summary>Marketplace whose costs are listed (costs are in its currency).</summary>
    public Domain.Core.Marketplace Marketplace { get; private set; } = default!;

    /// <summary>Whether the user may edit costs (shows the Edit links).</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Marketplace = (await currentMarketplace.GetAsync(cancellationToken)).Current;
        Products = await products.ListAsync(Marketplace.MarketplaceId, ToPageRequest(), cancellationToken);
    }
}
