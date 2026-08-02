using System.Globalization;

namespace AERai.Seller.SpApiClient;

/// <summary>
/// SP-API date-time query/body parameters expect strict ISO 8601 UTC with a literal 'Z' suffix
/// (e.g. "2020-01-01T00:00:00Z"), matching Amazon's documented examples exactly. DateTimeOffset's
/// round-trip "O" format instead emits a numeric "+00:00" offset and up to 7 fractional-second
/// digits, which the API has been observed to reject with a 400 — always format through here
/// instead of calling .ToString("O") directly on a value handed to SP-API.
/// </summary>
public static class SpApiDateTimeFormatter
{
    public static string ToIso8601Utc(DateTimeOffset value)
        => value.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
}
