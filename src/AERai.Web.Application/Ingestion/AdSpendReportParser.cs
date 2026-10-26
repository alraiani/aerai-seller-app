using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// Reads an Amazon Ads <c>spAdvertisedProduct</c> report (a JSON array, normally gzip-compressed) into one
/// row per (day, SKU, campaign). Shared by the sync and the blob refresh, so a report reads the same
/// whether it just arrived from Amazon or was loaded from raw storage later.
/// </summary>
public static class AdSpendReportParser
{
    /// <summary>The columns requested from Amazon, in the names the report returns them under.</summary>
    public static readonly IReadOnlyList<string> Columns =
    [
        "date", "advertisedSku", "advertisedAsin", "campaignId", "campaignName", "impressions", "clicks", "cost",
        "sales7d", "purchases7d", "unitsSoldClicks7d", "campaignBudgetCurrencyCode",
    ];

    /// <summary>Parses a report, compressed or not.</summary>
    /// <param name="content">The report's bytes; gzip is detected from its header, so plain JSON reads too.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>
    /// The rows. Rows without a SKU, date, or campaign are dropped, and rows Amazon splits by ad group are
    /// summed into their campaign, because spend is kept per SKU and campaign.
    /// </returns>
    /// <exception cref="JsonException">The content is not a JSON array of rows.</exception>
    public static async Task<IReadOnlyList<AdSpendRow>> ParseAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        buffer.Position = 0;

        var compressed = buffer.Length >= 2 && buffer.GetBuffer()[0] == 0x1f && buffer.GetBuffer()[1] == 0x8b;
        Stream json = compressed ? new GZipStream(buffer, CompressionMode.Decompress) : buffer;
        await using (json.ConfigureAwait(false))
        {
            using var document = await JsonDocument.ParseAsync(json, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("An Amazon Ads report must be a JSON array of rows.");
            }

            return Aggregate(document.RootElement.EnumerateArray().Select(ReadRow).OfType<AdSpendRow>());
        }
    }

    /// <summary>Longest SKU the catalog stores (Amazon's own limit is 40).</summary>
    private const int MaxSkuLength = 64;

    /// <summary>Longest campaign id stored (Amazon's are numbers of up to 20 digits).</summary>
    private const int MaxCampaignIdLength = 32;

    private static AdSpendRow? ReadRow(JsonElement row)
    {
        // A value too long to store would fail the whole report's write, so such a row is dropped instead.
        if (row.ValueKind != JsonValueKind.Object
            || Text(row, "advertisedSku") is not { Length: <= MaxSkuLength } sku
            || Text(row, "campaignId") is not { Length: <= MaxCampaignIdLength } campaignId
            || !DateOnly.TryParseExact(Text(row, "date"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return null;
        }

        return new AdSpendRow(
            date,
            sku,
            Text(row, "advertisedAsin"),
            campaignId,
            Text(row, "campaignName"),
            (long)Number(row, "impressions"),
            (long)Number(row, "clicks"),
            Number(row, "cost"),
            Number(row, "sales7d"),
            (int)Number(row, "purchases7d"),
            (int)Number(row, "unitsSoldClicks7d"),
            Text(row, "campaignBudgetCurrencyCode")?.ToUpperInvariant());
    }

    private static List<AdSpendRow> Aggregate(IEnumerable<AdSpendRow> rows) =>
        [.. rows
            .GroupBy(r => (r.Date, Sku: r.Sku.ToUpperInvariant(), r.CampaignId))
            .Select(g => g.First() with
            {
                Impressions = g.Sum(r => r.Impressions),
                Clicks = g.Sum(r => r.Clicks),
                Cost = g.Sum(r => r.Cost),
                AttributedSales = g.Sum(r => r.AttributedSales),
                AttributedOrders = g.Sum(r => r.AttributedOrders),
                AttributedUnits = g.Sum(r => r.AttributedUnits),
            })
            .OrderBy(r => r.Date)
            .ThenBy(r => r.Sku, StringComparer.OrdinalIgnoreCase)];

    // Amazon sends ids as numbers and names as strings; either is read as trimmed text.
    private static string? Text(JsonElement row, string name)
    {
        if (!row.TryGetProperty(name, out var value))
        {
            return null;
        }

        var text = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    // A metric Amazon leaves out or sends as null means none.
    private static decimal Number(JsonElement row, string name) =>
        row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number) ? number
        : row.TryGetProperty(name, out value) && value.ValueKind == JsonValueKind.String
            && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number) ? number
        : 0m;
}
