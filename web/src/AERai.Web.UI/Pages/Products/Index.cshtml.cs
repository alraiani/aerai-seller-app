using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Application.Products;
using AERai.Web.Application.Security;
using AERai.Web.UI.Models;

namespace AERai.Web.UI.Pages.Products;

/// <summary>
/// Product master data and cost of goods.
/// </summary>
/// <param name="products">Product queries.</param>
public sealed class IndexModel(IProductRepository products) : ListPageModel
{
    /// <summary>The current page of products.</summary>
    public PagedResult<ProductSummary> Products { get; private set; } = default!;

    /// <summary>Whether the user may edit costs (shows the Edit links).</summary>
    public bool CanEdit => User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Operator);

    /// <summary>Loads the requested page.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A task that completes when the page is loaded.</returns>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Products = await products.ListAsync(ToPageRequest(), cancellationToken);
    }
}
