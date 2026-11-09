using System.Globalization;
using System.Text.Json;
using AERai.Web.Application.Ingestion;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Parses a Finances API <c>listTransactions</c> page (v2024-06-19) into ledger transactions, each flattened to
/// its leaf breakdowns so its lines add up to its total. Shared by the live client and the simulator.
/// </summary>
/// <remarks>
/// <para>
/// Breakdowns nest to any depth (<c>AmazonFees</c> › <c>Commission</c>); only leaves are kept, as a path of
/// types. Amazon documents <c>breakdowns</c> as an array but its example wraps it in an object, so both shapes
/// are read. A transaction with items is taken from its items (the transaction-level breakdowns repeat them in
/// sum); one without items from its own breakdowns.
/// </para>
/// <para>
/// Whatever part of a total its leaves don't account for (a parent whose children don't add up, or a total
/// with no breakdowns) is kept as an <see cref="AmazonFinancialLine.Unitemized"/> line, so a transaction's lines
/// always equal its total and the ledger always adds up to the settlements it is paid in.
/// </para>
/// </remarks>
internal static class LedgerPageParser
{
    private const string PathSeparator = "/";

    /// <summary>Parses one response body.</summary>
    /// <param name="json">The body.</param>
    /// <returns>The page.</returns>
    /// <exception cref="InvalidOperationException">The body is not a <c>listTransactions</c> response.</exception>
    public static FinancialTransactionPage Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !TryGet(root, "payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"SP-API {SpApiOperation.ListTransactions} returned no payload.");
        }

        var transactions = new List<AmazonFinancialTransaction>();
        if (TryGet(payload, "transactions", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in list.EnumerateArray())
            {
                if (ReadTransaction(t) is { } transaction)
                {
                    transactions.Add(transaction);
                }
            }
        }

        var nextToken = TryGet(payload, "nextToken", out var token) && token.ValueKind == JsonValueKind.String ? token.GetString() : null;
        return new FinancialTransactionPage(transactions, string.IsNullOrEmpty(nextToken) ? null : nextToken, json);
    }

    private static AmazonFinancialTransaction? ReadTransaction(JsonElement t)
    {
        var id = Text(t, "transactionId");
        var posted = TryGet(t, "postedDate", out var p) && p.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(p.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
                ? parsed
                : (DateTimeOffset?)null;
        var (total, currency) = Money(t, "totalAmount");

        // Without an id or a date a transaction can't be kept or placed in time; without a currency its amount means nothing.
        if (string.IsNullOrWhiteSpace(id) || posted is null || string.IsNullOrWhiteSpace(currency))
        {
            return null;
        }

        var lines = new List<AmazonFinancialLine>();
        var items = TryGet(t, "items", out var itemList) && itemList.ValueKind == JsonValueKind.Array ? itemList.EnumerateArray().ToList() : [];
        if (items.Count > 0)
        {
            foreach (var item in items)
            {
                var product = Product(item) ?? Product(t);
                var itemLines = new List<AmazonFinancialLine>();
                AddLeaves(Breakdowns(item), prefix: null, product, itemLines);
                var (itemTotal, _) = Money(item, "totalAmount");
                var itemized = itemLines.Sum(l => l.Amount);
                if (itemTotal != itemized)
                {
                    itemLines.Add(new AmazonFinancialLine(product?.Sku, product?.Asin, 0, AmazonFinancialLine.Unitemized, itemTotal - itemized));
                }

                // The item's quantity sits on its first line only, so adding up lines counts each item once.
                if (itemLines.Count > 0 && product?.Quantity is > 0 and var quantity)
                {
                    itemLines[0] = itemLines[0] with { Units = quantity };
                }

                lines.AddRange(itemLines);
            }
        }
        else
        {
            AddLeaves(Breakdowns(t), prefix: null, Product(t), lines);
        }

        var accounted = lines.Sum(l => l.Amount);
        if (total != accounted)
        {
            lines.Add(new AmazonFinancialLine(null, null, 0, AmazonFinancialLine.Unitemized, total - accounted));
        }

        return new AmazonFinancialTransaction(
            id.Trim(),
            Text(t, "transactionType"),
            Text(t, "transactionStatus"),
            Text(t, "description"),
            posted.Value,
            Related(t, "ORDER_ID"),
            Related(t, "SETTLEMENT_ID"),
            total,
            currency.Trim().ToUpperInvariant(),
            lines);
    }

    /// <summary>Adds a breakdown tree's leaves; a parent whose children don't add up keeps the rest under its own path.</summary>
    private static void AddLeaves(IReadOnlyList<JsonElement> breakdowns, string? prefix, ProductInfo? product, List<AmazonFinancialLine> lines)
    {
        foreach (var b in breakdowns)
        {
            var type = Text(b, "breakdownType");
            var path = string.IsNullOrWhiteSpace(type) ? prefix ?? "(unnamed)" : prefix is null ? type.Trim() : prefix + PathSeparator + type.Trim();
            var (amount, _) = Money(b, "breakdownAmount");
            var children = Breakdowns(b);
            if (children.Count == 0)
            {
                if (amount != 0)
                {
                    lines.Add(new AmazonFinancialLine(product?.Sku, product?.Asin, 0, path, amount));
                }

                continue;
            }

            var before = lines.Count;
            AddLeaves(children, path, product, lines);
            var childSum = lines.Skip(before).Sum(l => l.Amount);
            if (amount != childSum)
            {
                lines.Add(new AmazonFinancialLine(product?.Sku, product?.Asin, 0, path, amount - childSum));
            }
        }
    }

    /// <summary>An element's breakdowns, whether Amazon sends an array or an object wrapping one.</summary>
    private static List<JsonElement> Breakdowns(JsonElement element)
    {
        if (!TryGet(element, "breakdowns", out var value))
        {
            return [];
        }

        if (value.ValueKind == JsonValueKind.Object && TryGet(value, "breakdowns", out var inner))
        {
            value = inner;
        }

        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToList() : [];
    }

    private static ProductInfo? Product(JsonElement element)
    {
        if (!TryGet(element, "contexts", out var contexts) || contexts.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var c in contexts.EnumerateArray())
        {
            var sku = Text(c, "sku");
            var asin = Text(c, "asin");
            if (sku is not null || asin is not null)
            {
                var quantity = TryGet(c, "quantityShipped", out var q) && q.ValueKind == JsonValueKind.Number && q.TryGetInt32(out var n) ? n : 0;
                return new ProductInfo(string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(), string.IsNullOrWhiteSpace(asin) ? null : asin.Trim(), quantity);
            }
        }

        return null;
    }

    private static string? Related(JsonElement t, string name)
    {
        if (!TryGet(t, "relatedIdentifiers", out var ids) || ids.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var r in ids.EnumerateArray())
        {
            if (string.Equals(Text(r, "relatedIdentifierName"), name, StringComparison.OrdinalIgnoreCase)
                && Text(r, "relatedIdentifierValue") is { } value && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static (decimal Amount, string? Currency) Money(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var money) || money.ValueKind != JsonValueKind.Object)
        {
            return (0m, null);
        }

        var amount = TryGet(money, "currencyAmount", out var a) && a.ValueKind == JsonValueKind.Number && a.TryGetDecimal(out var d) ? decimal.Round(d, 2) : 0m;
        return (amount, Text(money, "currencyCode"));
    }

    private static string? Text(JsonElement element, string name) =>
        TryGet(element, name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>Reads a property ignoring case (Amazon's casing is camelCase, but nothing depends on it).</summary>
    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }

        value = default;
        return false;
    }

    private sealed record ProductInfo(string? Sku, string? Asin, int Quantity);
}
