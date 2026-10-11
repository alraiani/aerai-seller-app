using System.Globalization;
using System.Text;
using AERai.Web.Application.Common;

namespace AERai.Web.Application.Inventory;

/// <summary>
/// Carries a reviewed count sheet's changes from the review page to Apply as plain text, one
/// <c>sku&lt;TAB&gt;current&lt;TAB&gt;new</c> line per SKU. A single form field keeps large sheets under
/// the form's field-count limit; Apply re-validates everything, so the text needs no protection
/// beyond what the uploader could already change.
/// </summary>
public static class HomeStockCountPayload
{
    /// <summary>Writes the changes.</summary>
    /// <param name="changes">The reviewed changes.</param>
    /// <returns>The payload text.</returns>
    public static string Write(IEnumerable<HomeStockCountChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var builder = new StringBuilder();
        foreach (var change in changes)
        {
            builder.Append(CultureInfo.InvariantCulture, $"{change.Sku}\t{change.Current}\t{change.New}\n");
        }

        return builder.ToString();
    }

    /// <summary>Reads the changes back.</summary>
    /// <param name="text">The payload text.</param>
    /// <returns>The changes, or a failure when a line is malformed or out of range.</returns>
    public static Result<IReadOnlyList<HomeStockCountChange>> Read(string? text)
    {
        var changes = new List<HomeStockCountChange>();
        foreach (var line in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split('\t');
            if (parts.Length != 3
                || parts[0].Length is 0 or > 64
                || !TryCount(parts[1], out var current)
                || !TryCount(parts[2], out var updated))
            {
                return Result.Failure<IReadOnlyList<HomeStockCountChange>>("The reviewed changes could not be read. Upload the sheet again.");
            }

            changes.Add(new HomeStockCountChange(parts[0], current, updated));
        }

        return changes.Count == 0
            ? Result.Failure<IReadOnlyList<HomeStockCountChange>>("There are no changes to apply.")
            : Result.Success<IReadOnlyList<HomeStockCountChange>>(changes);
    }

    private static bool TryCount(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value) && value <= InventoryItemService.MaxHomeStock;
}
