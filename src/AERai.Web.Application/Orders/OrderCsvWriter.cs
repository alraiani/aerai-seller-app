using System.Globalization;
using System.Text;

namespace AERai.Web.Application.Orders;

/// <summary>Writes the Orders CSV: one row per order item, RFC 4180 quoting, CRLF line endings.</summary>
public static class OrderCsvWriter
{
    private static readonly string?[] Headers =
        ["order-id", "purchase-date", "order-status", "sku", "asin", "title", "quantity", "item-price", "currency", "unit-cost"];

    /// <summary>Writes the rows with a header line.</summary>
    /// <param name="rows">The rows, in the order to write them.</param>
    /// <param name="zone">The marketplace's time zone: purchase dates are written in its local time, with their offset.</param>
    /// <returns>The CSV text.</returns>
    public static string Write(IEnumerable<OrderExportRow> rows, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(zone);

        var csv = new StringBuilder();
        AppendLine(csv, Headers);
        foreach (var row in rows)
        {
            AppendLine(csv,
            [
                row.AmazonOrderId,
                TimeZoneInfo.ConvertTime(row.PurchaseDate, zone).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture),
                row.OrderStatus,
                row.Sku,
                row.Asin,
                row.Title,
                row.Quantity.ToString(CultureInfo.InvariantCulture),
                row.ItemPrice.ToString("0.00", CultureInfo.InvariantCulture),
                row.Currency,
                row.UnitCost?.ToString("0.00", CultureInfo.InvariantCulture),
            ]);
        }

        return csv.ToString();
    }

    /// <summary>Quotes a field when it holds a comma, quote, or line break (quotes doubled), per RFC 4180.</summary>
    /// <param name="value">The field, or <see langword="null"/> for an empty one.</param>
    /// <returns>The field as written.</returns>
    public static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Text starting with =, +, -, or @ runs as a formula when the file is opened in a spreadsheet, and
        // titles are free text, so such text gets a leading apostrophe. Negative numbers are left alone.
        if (value[0] is '=' or '+' or '@' || (value[0] == '-' && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)))
        {
            value = "'" + value;
        }

        return value.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : value;
    }

    private static void AppendLine(StringBuilder csv, string?[] fields)
    {
        for (var i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                csv.Append(',');
            }

            csv.Append(Escape(fields[i]));
        }

        csv.Append("\r\n");
    }
}
