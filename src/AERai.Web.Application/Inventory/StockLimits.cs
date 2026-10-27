namespace AERai.Web.Application.Inventory;

/// <summary>
/// What keeps a send from piling up stock at Amazon that will age into storage fees: Amazon's own estimate of excess
/// units, and the most days of expected sales Amazon should hold.
/// </summary>
/// <param name="ExcessAtAmazon">Units Amazon estimates are more than will sell in time (0 = none or unknown).</param>
/// <param name="MaxAmazonCoverDays">Most days of expected sales stock at Amazon may cover after a send (0 = no cap).</param>
public sealed record StockLimits(int ExcessAtAmazon, int MaxAmazonCoverDays)
{
    /// <summary>No limits.</summary>
    public static StockLimits None { get; } = new(0, 0);
}
