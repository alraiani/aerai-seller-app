using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>The editable fields of an inventory item, as submitted.</summary>
/// <param name="Family">Family name; blank for none (a new name creates the family).</param>
/// <param name="HomeStock">Units held outside Amazon for the marketplace.</param>
/// <param name="LeadTimes">The SKU's own lead-time overrides; blank fields use the defaults.</param>
/// <param name="Color">The variant's color, if set.</param>
public sealed record InventoryItemUpdate(string? Family, int HomeStock, LeadTimeSettings LeadTimes, ProductColor? Color = null);
