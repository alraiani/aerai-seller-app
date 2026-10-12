namespace AERai.Web.Domain.Core;

/// <summary>
/// Bookkeeping configuration for one marketplace.
/// </summary>
/// <remarks>
/// Each marketplace is paid in its own currency, usually into its own bank account, so the deposit
/// account is per marketplace. A marketplace with no row has no deposit account yet and cannot export.
/// </remarks>
public sealed class BookkeepingSettings
{
    /// <summary>Marketplace the settings belong to (primary key).</summary>
    public required string MarketplaceId { get; set; }

    /// <summary>Exact name of the QuickBooks account the settlement payouts are deposited into.</summary>
    public required string DepositAccountName { get; set; }

    /// <summary>When the settings last changed.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Email of the user who last changed them.</summary>
    public required string UpdatedBy { get; set; }
}
