namespace AERai.Web.Application.Inventory;

/// <summary>
/// How busy each calendar month is compared with an average month, learned from past sales (weddings make
/// spring and summer much busier than winter). 1.0 everywhere when there isn't a full year of history.
/// </summary>
public sealed class SeasonalityIndex
{
    /// <summary>Full months of history needed before the index is learned (every calendar month seen once).</summary>
    public const int MinMonths = 12;

    /// <summary>Most recent full months the index learns from (two years, so one odd year weighs less).</summary>
    public const int MaxMonths = 24;

    /// <summary>Lowest factor a month can get, so a quiet month never plans for (almost) nothing.</summary>
    public const decimal MinFactor = 0.4m;

    /// <summary>Highest factor a month can get, so one freak month doesn't flood Amazon.</summary>
    public const decimal MaxFactor = 2.5m;

    private readonly decimal[] _factors;

    private SeasonalityIndex(decimal[] factors, bool isLearned)
    {
        _factors = factors;
        IsLearned = isLearned;
    }

    /// <summary>No seasonality: every month counts the same.</summary>
    public static SeasonalityIndex Flat { get; } = new([.. Enumerable.Repeat(1m, 12)], false);

    /// <summary>Whether the factors were learned from enough history (otherwise every factor is 1).</summary>
    public bool IsLearned { get; }

    /// <summary>The factor for a calendar month.</summary>
    /// <param name="month">1–12.</param>
    /// <returns>Its factor (1 = an average month).</returns>
    public decimal ForMonth(int month) => _factors[month - 1];

    /// <summary>The factor for a day (its month's).</summary>
    /// <param name="day">The day.</param>
    /// <returns>Its factor.</returns>
    public decimal On(DateOnly day) => _factors[day.Month - 1];

    /// <summary>The average factor over a run of days.</summary>
    /// <param name="from">First day.</param>
    /// <param name="days">How many days.</param>
    /// <returns>The mean factor (1 for no days).</returns>
    public decimal Average(DateOnly from, int days)
    {
        if (days <= 0)
        {
            return 1m;
        }

        var total = 0m;
        for (var i = 0; i < days; i++)
        {
            total += On(from.AddDays(i));
        }

        return total / days;
    }

    /// <summary>
    /// Learns the index from monthly unit sales: each calendar month's average over the last
    /// <see cref="MaxMonths"/> full months, divided by the average month, lightly smoothed with its neighbors
    /// (a quarter each) so one unusual month doesn't swing the plan, and clamped to
    /// <see cref="MinFactor"/>–<see cref="MaxFactor"/>.
    /// </summary>
    /// <param name="monthlyUnits">Units sold per (year, month); months missing in between count as zero.</param>
    /// <param name="today">Today; its month is still running, so it isn't used.</param>
    /// <returns>The index, or <see cref="Flat"/> with fewer than <see cref="MinMonths"/> full months since the first sale.</returns>
    public static SeasonalityIndex From(IEnumerable<(int Year, int Month, int Units)> monthlyUnits, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(monthlyUnits);

        var thisMonth = MonthNumber(today.Year, today.Month);
        var byMonth = monthlyUnits
            .GroupBy(m => MonthNumber(m.Year, m.Month))
            .Where(g => g.Key < thisMonth && g.Key >= thisMonth - MaxMonths)
            .ToDictionary(g => g.Key, g => g.Sum(m => m.Units));
        if (byMonth.Count == 0 || byMonth.Values.Sum() <= 0)
        {
            return Flat;
        }

        var first = byMonth.Keys.Min();
        if (thisMonth - first < MinMonths)
        {
            return Flat;
        }

        // Average per calendar month over the full months since the first sale (gaps are months with no sales).
        var sums = new decimal[12];
        var counts = new int[12];
        for (var m = first; m < thisMonth; m++)
        {
            sums[m % 12] += byMonth.GetValueOrDefault(m);
            counts[m % 12]++;
        }

        var averages = Enumerable.Range(0, 12).Select(i => counts[i] == 0 ? 0m : sums[i] / counts[i]).ToArray();
        var mean = averages.Average();
        if (mean <= 0)
        {
            return Flat;
        }

        var factors = new decimal[12];
        for (var i = 0; i < 12; i++)
        {
            var smoothed = (averages[i] * 2 + averages[(i + 11) % 12] + averages[(i + 1) % 12]) / 4;
            factors[i] = Math.Round(Math.Clamp(smoothed / mean, MinFactor, MaxFactor), 2);
        }

        return new SeasonalityIndex(factors, true);
    }

    // Months counted from year 0, so consecutive months differ by one; index % 12 is January = 0.
    private static int MonthNumber(int year, int month) => (year * 12) + (month - 1);
}
