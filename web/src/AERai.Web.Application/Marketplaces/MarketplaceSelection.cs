using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Marketplaces;

/// <summary>The marketplace the current user is viewing, plus every marketplace for the switcher.</summary>
/// <param name="Current">Active marketplace that pages report on.</param>
/// <param name="All">All marketplaces in switcher order, including inactive ones (shown as not set up).</param>
public sealed record MarketplaceSelection(Marketplace Current, IReadOnlyList<Marketplace> All)
{
    /// <summary>
    /// Picks the requested marketplace when it is active; otherwise the first active one, so a stale
    /// or tampered preference (unknown id, or a marketplace since deactivated) never breaks a page.
    /// </summary>
    /// <param name="all">All marketplaces in switcher order.</param>
    /// <param name="requestedId">The user's remembered choice, if any.</param>
    /// <returns>The selection.</returns>
    /// <exception cref="InvalidOperationException">No marketplace is active.</exception>
    public static MarketplaceSelection Resolve(IReadOnlyList<Marketplace> all, string? requestedId)
    {
        ArgumentNullException.ThrowIfNull(all);

        var current = all.FirstOrDefault(m => m.IsActive && string.Equals(m.MarketplaceId, requestedId, StringComparison.Ordinal))
            ?? all.FirstOrDefault(m => m.IsActive)
            ?? throw new InvalidOperationException("No marketplace is active. Activate one in core.Marketplace.");

        return new MarketplaceSelection(current, all);
    }
}
