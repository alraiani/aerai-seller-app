namespace AERai.Web.Application.Inventory;

/// <summary>
/// What to do to keep a SKU in stock: how many home-stock units to send into Amazon and by when
/// (weighing Amazon's own recommendation), and when to reorder from the supplier.
/// </summary>
/// <param name="ProjectedStockout">The day stock at Amazon runs out at the current sales rate.</param>
/// <param name="UnitsNeeded">Units Amazon needs to reach the target cover by our own math (0 = enough already).</param>
/// <param name="SendToAmazon">
/// Units to ship from home stock: the larger of <paramref name="UnitsNeeded"/> and Amazon's
/// recommendation, capped at what is on hand.
/// </param>
/// <param name="SendBy">
/// Last day to ship them: the earlier of our deadline and Amazon's recommended ship date, or
/// <see langword="null"/> when nothing needs sending.
/// </param>
/// <param name="ShortAtHome">Units that should go to Amazon but aren't at home (0 = home stock covers it).</param>
/// <param name="Amazon">Amazon's recommendation, when its restock report covers the SKU.</param>
/// <param name="ReorderQuantity">Units to order from the supplier at the next reorder.</param>
/// <param name="ReorderBy">
/// Last day to place that order so it arrives (through prep and transit) before all stock — at
/// Amazon and at home — runs out, keeping the safety buffer.
/// </param>
/// <param name="DaysUntilReorder">Days from today to <paramref name="ReorderBy"/> (negative = overdue).</param>
/// <param name="DaysUntilAction">
/// Days from today to the earliest of the send and reorder deadlines (negative = overdue).
/// </param>
public sealed record RestockPlan(
    DateOnly ProjectedStockout,
    int UnitsNeeded,
    int SendToAmazon,
    DateOnly? SendBy,
    int ShortAtHome,
    AmazonRecommendation? Amazon,
    int ReorderQuantity,
    DateOnly ReorderBy,
    int DaysUntilReorder,
    int DaysUntilAction);
