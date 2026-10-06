using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>Reads the marketplace reference data.</summary>
public interface IMarketplaceQueries
{
    /// <summary>All marketplaces, active or not, in switcher order.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The marketplaces.</returns>
    Task<IReadOnlyList<Marketplace>> GetAllAsync(CancellationToken cancellationToken);
}
