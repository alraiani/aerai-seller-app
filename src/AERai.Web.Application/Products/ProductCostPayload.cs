using System.Text;
using AERai.Web.Application.Common;

namespace AERai.Web.Application.Products;

/// <summary>
/// Carries a reviewed cost sheet's changes from the review page to Apply as plain text, one
/// <c>sku&lt;TAB&gt;current&lt;TAB&gt;new</c> line per SKU (an empty field means no cost). A single
/// form field keeps large sheets under the form's field-count limit; Apply re-validates everything,
/// so the text needs no protection beyond what the uploader could already change.
/// </summary>
public static class ProductCostPayload
{
    /// <summary>Writes the changes.</summary>
    /// <param name="changes">The reviewed changes.</param>
    /// <returns>The payload text.</returns>
    public static string Write(IEnumerable<ProductCostChange> changes)
    {
        ArgumentNullException.ThrowIfNull(changes);

        var builder = new StringBuilder();
        foreach (var change in changes)
        {
            builder.Append(change.Sku).Append('\t')
                .Append(ProductCostText.Format(change.Current)).Append('\t')
                .Append(ProductCostText.Format(change.New)).Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>Reads the changes back.</summary>
    /// <param name="text">The payload text.</param>
    /// <returns>The changes, or a failure when a line is malformed or out of range.</returns>
    public static Result<IReadOnlyList<ProductCostChange>> Read(string? text)
    {
        var changes = new List<ProductCostChange>();
        foreach (var line in (text ?? string.Empty).Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.TrimEnd('\r').Split('\t');
            if (parts.Length != 3
                || parts[0].Trim().Length is 0 or > 64
                || !ProductCostText.TryParse(parts[1], out var current)
                || !ProductCostText.TryParse(parts[2], out var updated))
            {
                return Result.Failure<IReadOnlyList<ProductCostChange>>("The reviewed changes could not be read. Upload the sheet again.");
            }

            changes.Add(new ProductCostChange(parts[0].Trim(), current, updated));
        }

        return changes.Count == 0
            ? Result.Failure<IReadOnlyList<ProductCostChange>>("There are no changes to apply.")
            : Result.Success<IReadOnlyList<ProductCostChange>>(changes);
    }
}
