namespace AERai.Web.Application.Ingestion;

/// <summary>One SKU's stock in Amazon Warehousing and Distribution (AWD), as Amazon lists it.</summary>
/// <param name="Sku">Seller SKU.</param>
/// <param name="OnHand">Units physically in AWD distribution centers.</param>
/// <param name="Inbound">Units on their way from the seller to AWD.</param>
/// <param name="AvailableDistributable">On-hand units free to be replenished into FBA.</param>
/// <param name="ReservedDistributable">On-hand units set aside for replenishment orders being prepared.</param>
/// <param name="Replenishment">Units in transit from AWD to FBA.</param>
public sealed record AwdInventoryItem(
    string Sku,
    int OnHand,
    int Inbound,
    int AvailableDistributable,
    int ReservedDistributable,
    int Replenishment)
{
    /// <summary>Whether every quantity is zero (Amazon keeps listing SKUs that have left AWD).</summary>
    public bool IsEmpty => OnHand == 0 && Inbound == 0 && AvailableDistributable == 0 && ReservedDistributable == 0 && Replenishment == 0;
}
