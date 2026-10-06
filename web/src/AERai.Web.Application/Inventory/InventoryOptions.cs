using System.ComponentModel.DataAnnotations;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// Restock-planning defaults, used for any SKU (or field) without its own lead-time profile. Bound
/// from the <c>Inventory</c> configuration section.
/// </summary>
public sealed class InventoryOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Inventory";

    /// <summary>Default days from ordering to the goods arriving.</summary>
    [Range(0, 365)]
    public int SupplierLeadTimeDays { get; set; } = 30;

    /// <summary>Default days to prepare goods for shipping.</summary>
    [Range(0, 365)]
    public int PrepTimeDays { get; set; } = 7;

    /// <summary>Default days from shipping to Amazon until sellable.</summary>
    [Range(0, 365)]
    public int TransitDays { get; set; } = 10;

    /// <summary>Default buffer kept before the projected stockout.</summary>
    [Range(0, 365)]
    public int SafetyStockDays { get; set; } = 14;

    /// <summary>Default days of sales a restock should cover.</summary>
    [Range(1, 730)]
    public int TargetStockDays { get; set; } = 90;

    /// <summary>A restock action due within this many days counts as "soon" (highlighted, and alerted on).</summary>
    [Range(0, 90)]
    public int AlertLeadDays { get; set; } = 7;

    /// <summary>The defaults as <see cref="LeadTimes"/>.</summary>
    /// <returns>The default timings.</returns>
    public LeadTimes Defaults() => new(SupplierLeadTimeDays, PrepTimeDays, TransitDays, SafetyStockDays, TargetStockDays, IsCustom: false);

    /// <summary>A SKU's timings: its own values where set, the defaults elsewhere.</summary>
    /// <param name="settings">The SKU's overrides, or <see langword="null"/> when it has none.</param>
    /// <returns>The effective timings.</returns>
    public LeadTimes Resolve(LeadTimeSettings? settings) => settings is null || settings.IsEmpty
        ? Defaults()
        : new(
            settings.SupplierLeadTimeDays ?? SupplierLeadTimeDays,
            settings.PrepTimeDays ?? PrepTimeDays,
            settings.TransitDays ?? TransitDays,
            settings.SafetyStockDays ?? SafetyStockDays,
            settings.TargetStockDays ?? TargetStockDays,
            IsCustom: true);
}
