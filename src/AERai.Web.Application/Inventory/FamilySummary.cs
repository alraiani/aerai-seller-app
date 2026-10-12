namespace AERai.Web.Application.Inventory;

/// <summary>A product family with how many SKUs belong to it.</summary>
/// <param name="Id">Family id.</param>
/// <param name="Name">Display name.</param>
/// <param name="SkuCount">SKUs in the family (across every marketplace; families are shared).</param>
public sealed record FamilySummary(int Id, string Name, int SkuCount);
