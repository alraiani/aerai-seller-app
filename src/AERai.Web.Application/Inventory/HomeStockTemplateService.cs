using System.Globalization;
using AERai.Web.Application.Abstractions;
using AERai.Web.Application.Common;
using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Inventory;

/// <summary>Default <see cref="IHomeStockTemplateService"/>.</summary>
/// <param name="inventory">Inventory numbers (stock, sales, restock plans).</param>
/// <param name="writer">Workbook renderer.</param>
/// <param name="clock">Clock (dates the file name).</param>
public sealed class HomeStockTemplateService(IInventoryService inventory, IHomeStockTemplateWriter writer, TimeProvider clock) : IHomeStockTemplateService
{
    /// <inheritdoc/>
    public async Task<(string FileName, byte[] Content)> CreateAsync(Marketplace marketplace, int? familyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(marketplace);

        var items = await inventory.GetItemsAsync(marketplace, cancellationToken).ConfigureAwait(false);

        // Laid out like the worksheet (family, then color in palette order, then SKU) so a count
        // taken shelf by shelf follows the same order; SKUs without a family or color come last.
        var rows = items
            .Where(i => familyId is null || i.Position.FamilyId == familyId)
            .OrderBy(i => i.Position.Family is null)
            .ThenBy(i => i.Position.Family, StringComparer.OrdinalIgnoreCase)
            .ThenBy(i => i.Position.Color is null)
            .ThenBy(i => i.Position.Color)
            .ThenBy(i => i.Sku, StringComparer.Ordinal)
            .Select(i => new HomeStockTemplateRow(
                i.Sku,
                i.Position.HomeStock,
                i.Position.Title,
                i.Position.Family,
                i.Position.Color,
                i.Position.Available,
                i.Position.Inbound,
                i.UnitsSold30d,
                i.Restock is { SendToAmazon: > 0 } plan ? plan.SendToAmazon : null))
            .ToList();

        var family = familyId is null ? null : rows.FirstOrDefault()?.Family;
        var title = family is null ? $"Home stock — {marketplace.Name}" : $"Home stock — {family}, {marketplace.Name}";
        var today = LocalTime.DateOf(clock.GetUtcNow(), TimeZoneInfo.FindSystemTimeZoneById(marketplace.TimeZoneId));
        var fileName = string.Create(CultureInfo.InvariantCulture, $"home-stock-{marketplace.Code}{(family is null ? "" : "-" + FileNames.Slug(family))}-{today:yyyy-MM-dd}.xlsx");
        return (fileName, writer.Write(title, rows));
    }
}
