using AERai.Web.Application.Ingestion;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Abstractions;

/// <summary>
/// Amazon Warehousing and Distribution (AWD) inventory. The live implementation calls the SP-API AWD
/// API (<c>listInventory</c>, v2024-05-09) through the SP-API pipeline; the simulated one makes up
/// stock for local development.
/// </summary>
public interface IAmazonAwdGateway
{
    /// <summary>Gets one page of the seller's AWD inventory.</summary>
    /// <param name="marketplace">Marketplace being synced; its region selects the endpoint and credentials.</param>
    /// <param name="nextToken">Token from the previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The page, with the token for the next one.</returns>
    Task<AwdInventoryPage> ListInventoryPageAsync(Marketplace marketplace, string? nextToken, CancellationToken cancellationToken);
}
