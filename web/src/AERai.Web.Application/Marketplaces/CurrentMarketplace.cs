using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;

namespace AERai.Web.Application.Marketplaces;

/// <summary>
/// Default <see cref="ICurrentMarketplace"/>: validates the user's remembered choice against the
/// marketplace list. Scoped, so the list is read at most once per request.
/// </summary>
/// <param name="marketplaces">Marketplace reference data.</param>
/// <param name="preference">The user's remembered choice.</param>
public sealed class CurrentMarketplace(IMarketplaceQueries marketplaces, IMarketplacePreference preference) : ICurrentMarketplace
{
    private MarketplaceSelection? _selection;

    /// <inheritdoc/>
    public async Task<MarketplaceSelection> GetAsync(CancellationToken cancellationToken)
    {
        if (_selection is null)
        {
            var all = await marketplaces.GetAllAsync(cancellationToken).ConfigureAwait(false);
            _selection = MarketplaceSelection.Resolve(all, preference.Read());
        }

        return _selection;
    }

    /// <inheritdoc/>
    public async Task<Result> SelectAsync(string marketplaceId, CancellationToken cancellationToken)
    {
        var all = await marketplaces.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var target = all.FirstOrDefault(m => string.Equals(m.MarketplaceId, marketplaceId, StringComparison.Ordinal));
        if (target is null)
        {
            return Result.Failure("That marketplace does not exist.");
        }

        if (!target.IsActive)
        {
            return Result.Failure($"{target.Name} is not set up yet.");
        }

        preference.Write(target.MarketplaceId);
        _selection = new MarketplaceSelection(target, all);
        return Result.Success();
    }
}
