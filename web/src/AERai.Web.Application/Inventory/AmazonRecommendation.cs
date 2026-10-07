namespace AERai.Web.Application.Inventory;

/// <summary>Amazon's advice from its restock inventory report.</summary>
/// <param name="Quantity">Units Amazon recommends sending in (0 = none).</param>
/// <param name="ShipDate">The date Amazon recommends shipping by, when it gives one.</param>
public sealed record AmazonRecommendation(int Quantity, DateOnly? ShipDate)
{
    /// <summary>Amazon's advice for a SKU, or <see langword="null"/> when its report doesn't cover it.</summary>
    /// <param name="quantity">Recommended units, or <see langword="null"/> when there is no report row.</param>
    /// <param name="shipDate">Recommended ship date.</param>
    /// <returns>The recommendation, or <see langword="null"/>.</returns>
    public static AmazonRecommendation? From(int? quantity, DateOnly? shipDate) =>
        quantity is { } units ? new AmazonRecommendation(units, shipDate) : null;
}
