using AERai.Web.Application.Inventory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace AERai.Web.UI.Pages.Inventory;

/// <summary>
/// Serves a product's picture to signed-in users. Pictures are streamed through the app because the
/// storage account allows no public or shared-key access.
/// </summary>
/// <param name="items">Item service.</param>
public sealed class ImageModel(IInventoryItemService items) : PageModel
{
    /// <summary>Streams the picture.</summary>
    /// <param name="sku">Seller SKU from the route.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The picture, or 404 when the SKU has none.</returns>
    public async Task<IActionResult> OnGetAsync(string sku, CancellationToken cancellationToken)
    {
        if (await items.OpenImageAsync(sku, cancellationToken) is not { } image)
        {
            return NotFound();
        }

        // Every upload gets a new blob path, which links carry as ?v=, so a URL's bytes never change
        // and the browser can keep it. Private: pictures are behind sign-in.
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        return File(image.Content, image.ContentType);
    }
}
