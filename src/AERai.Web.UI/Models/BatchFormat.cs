using AERai.Web.Application.Imports;

namespace AERai.Web.UI.Models;

/// <summary>Display helpers for import batches.</summary>
public static class BatchFormat
{
    /// <summary>The dates a batch's file covers, as <c>2026-03-01 to 2026-03-14</c> (one date when it is a single day).</summary>
    /// <param name="batch">The batch.</param>
    /// <returns>The range, or an em dash when no date in the file could be read.</returns>
    public static string DataRange(ImportBatchSummary batch)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.DataStart is not { } start || batch.DataEnd is not { } end)
        {
            return "—";
        }

        var first = start.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var last = end.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        return first == last ? first : $"{first} to {last}";
    }
}
