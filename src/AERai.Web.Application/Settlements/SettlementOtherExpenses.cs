namespace AERai.Web.Application.Settlements;

/// <summary>
/// Explains a settlement's Other expenses column: the charges in it by name, largest first, so a
/// reader can see what is behind the number (on the Summary page, as a tooltip).
/// </summary>
public static class SettlementOtherExpenses
{
    /// <summary>How many charges are named before the rest are added up as one.</summary>
    public const int MaxItems = 8;

    /// <summary>Label of the item that adds up whatever is past <see cref="MaxItems"/>.</summary>
    public const string RestLabel = "Everything else";

    /// <summary>Amazon descriptions that say nothing on their own; the amount type names the charge instead.</summary>
    private static readonly HashSet<string> GenericDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        "TransactionTotalAmount",
        "Base fee",
        "Discount on Fee",
    };

    /// <summary>The charges behind each per-SKU summary row.</summary>
    /// <param name="lines">The settlement's Other expenses lines, added up by kind.</param>
    /// <param name="amounts">Which currency the page shows (converted only where the lines have a rate).</param>
    /// <returns>The items of each row, keyed by its SKU and price per item (as paid).</returns>
    public static IReadOnlyDictionary<(string? Sku, decimal? SalePrice), IReadOnlyList<SettlementExpenseItem>> ByRow(
        IReadOnlyList<SettlementExpenseLine> lines, SettlementAmounts amounts)
    {
        ArgumentNullException.ThrowIfNull(lines);
        return lines
            .GroupBy(l => (l.Sku, l.SalePrice))
            .ToDictionary(g => g.Key, g => Items(g, amounts));
    }

    /// <summary>The charges behind the whole settlement.</summary>
    /// <param name="lines">The settlement's Other expenses lines, added up by kind.</param>
    /// <param name="amounts">Which currency the page shows (converted only where the lines have a rate).</param>
    /// <returns>The items, largest first.</returns>
    public static IReadOnlyList<SettlementExpenseItem> Items(IEnumerable<SettlementExpenseLine> lines, SettlementAmounts amounts)
    {
        ArgumentNullException.ThrowIfNull(lines);
        var items = lines
            .GroupBy(l => LabelOf(l.TransactionType, l.AmountType, l.AmountDescription), StringComparer.OrdinalIgnoreCase)
            .Select(g => new SettlementExpenseItem(g.Key, g.Sum(l => AmountOf(l, amounts))))
            .Where(i => i.Amount != 0)
            // Expenses are negative: the biggest charge first, credits last.
            .OrderBy(i => i.Amount)
            .ThenBy(i => i.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (items.Count <= MaxItems)
        {
            return items;
        }

        var named = items.Take(MaxItems - 1).ToList();
        named.Add(new SettlementExpenseItem(RestLabel, items.Skip(MaxItems - 1).Sum(i => i.Amount)));
        return named;
    }

    /// <summary>What a kind of settlement line is for, in words.</summary>
    /// <param name="transactionType">Amazon's transaction type.</param>
    /// <param name="amountType">Amazon's amount type.</param>
    /// <param name="amountDescription">Amazon's amount description.</param>
    /// <returns>A label such as "Advertising", "Promotions", or "Subscription Fee".</returns>
    /// <remarks>
    /// Amazon's credits for advertising are listed as their own amount type; they share the label of
    /// the charge so the tooltip shows what advertising cost in the end.
    /// </remarks>
    public static string LabelOf(string transactionType, string? amountType, string? amountDescription)
    {
        if (amountType is "Cost of Advertising" or "Refund for Advertiser")
        {
            return "Advertising";
        }

        if (amountType is "Promotion")
        {
            return "Promotions";
        }

        if (amountType is "FBA Inventory Reimbursement")
        {
            return "Reimbursement clawbacks";
        }

        if (!string.IsNullOrWhiteSpace(amountDescription) && !GenericDescriptions.Contains(amountDescription))
        {
            return amountDescription;
        }

        return string.IsNullOrWhiteSpace(amountType) ? transactionType : amountType;
    }

    private static decimal AmountOf(SettlementExpenseLine line, SettlementAmounts amounts) =>
        amounts == SettlementAmounts.Home && line.HomeAmount is { } home ? home : line.Amount;
}
