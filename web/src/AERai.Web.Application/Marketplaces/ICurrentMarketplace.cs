using AERai.Web.Application.Common;

namespace AERai.Web.Application.Marketplaces;

/// <summary>
/// The marketplace the signed-in user is viewing. Every marketplace-scoped page asks this for its
/// marketplace instead of reading the preference directly.
/// </summary>
public interface ICurrentMarketplace
{
    /// <summary>Resolves the current marketplace (cached for the rest of the request).</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current marketplace and the full list.</returns>
    Task<MarketplaceSelection> GetAsync(CancellationToken cancellationToken);

    /// <summary>Switches the user to another marketplace.</summary>
    /// <param name="marketplaceId">Amazon marketplace id to switch to.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a failure when the id is unknown or the marketplace is not active.</returns>
    Task<Result> SelectAsync(string marketplaceId, CancellationToken cancellationToken);
}
