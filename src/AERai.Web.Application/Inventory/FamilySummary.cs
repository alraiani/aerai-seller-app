namespace AERai.Web.Application.Inventory;

/// <summary>A product family with how many SKUs belong to it.</summary>
/// <param name="Id">Family id.</param>
/// <param name="Name">Display name.</param>
/// <param name="SkuCount">SKUs in the family (across every marketplace; families are shared).</param>
/// <param name="Rules">How its sends and supplier orders are rounded (<see cref="RestockRules.None"/> when unset).</param>
public sealed record FamilySummary(int Id, string Name, int SkuCount, RestockRules? Rules = null)
{
    /// <summary>The family's rules, never null.</summary>
    public RestockRules RulesOrNone => Rules ?? RestockRules.None;
}
