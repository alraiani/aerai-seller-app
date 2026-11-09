namespace AERai.Web.Application.Profit;

/// <summary>Splits an amount across rows by weight, to the cent, so the parts always add up to it exactly.</summary>
public static class ProfitAllocator
{
    /// <summary>
    /// Splits <paramref name="amount"/> in proportion to <paramref name="weights"/> (largest remainder: each part is
    /// rounded down to the cent, and the cents left go to the parts that lost the most).
    /// </summary>
    /// <param name="amount">The amount (any sign), in currency units.</param>
    /// <param name="weights">One non-negative weight per part.</param>
    /// <returns>
    /// One part per weight, adding up to <paramref name="amount"/> rounded to the cent; <see langword="null"/> when no
    /// weight is positive, because there is nothing to split by.
    /// </returns>
    public static IReadOnlyList<decimal>? Split(decimal amount, IReadOnlyList<decimal> weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        var total = weights.Where(w => w > 0).Sum();
        if (total <= 0)
        {
            return null;
        }

        var cents = (long)decimal.Round(Math.Abs(amount) * 100m, 0, MidpointRounding.AwayFromZero);
        var sign = amount < 0 ? -1 : 1;
        var exact = weights.Select(w => w > 0 ? cents * (w / total) : 0m).ToList();
        var parts = exact.Select(e => (long)decimal.Floor(e)).ToArray();
        var left = cents - parts.Sum();
        foreach (var index in exact.Select((e, i) => (Remainder: e - decimal.Floor(e), Index: i))
                     .OrderByDescending(x => x.Remainder)
                     .ThenBy(x => x.Index)
                     .Take((int)left)
                     .Select(x => x.Index))
        {
            parts[index]++;
        }

        return [.. parts.Select(p => sign * p / 100m)];
    }
}
