namespace AERai.Web.Application.Inventory;

/// <summary>
/// What to do to keep a SKU in stock: how many units to send from home stock and to order from the
/// supplier, and the last day each can happen and still arrive before the safety buffer runs out.
/// </summary>
/// <param name="ProjectedStockout">The day sellable stock runs out at the current sales rate.</param>
/// <param name="UnitsNeeded">Units to add to reach the target cover (0 = enough already).</param>
/// <param name="SendFromHome">Units to ship from home stock (at most what is on hand).</param>
/// <param name="SendBy">Last day to ship home stock to Amazon.</param>
/// <param name="OrderFromSupplier">Units home stock cannot cover, to order from the supplier.</param>
/// <param name="OrderBy">Last day to place the supplier order.</param>
/// <param name="DaysUntilAction">
/// Days from today to the earliest action with units to move (negative = overdue), or
/// <see langword="null"/> when nothing needs doing.
/// </param>
public sealed record RestockPlan(
    DateOnly ProjectedStockout,
    int UnitsNeeded,
    int SendFromHome,
    DateOnly SendBy,
    int OrderFromSupplier,
    DateOnly OrderBy,
    int? DaysUntilAction);
