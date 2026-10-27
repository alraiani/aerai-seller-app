namespace AERai.Web.Application.Inventory;

/// <summary>
/// Expected unit sales per day ahead: a season-free base rate scaled by each day's month in a
/// <see cref="SeasonalityIndex"/>.
/// </summary>
/// <param name="BaseRate">Units per day in an average month.</param>
/// <param name="Season">How each month compares with an average one.</param>
public sealed record DemandForecast(decimal BaseRate, SeasonalityIndex Season)
{
    /// <summary>Longest run of days counted when working out how long stock lasts (ten years: "never").</summary>
    public const int MaxDays = 3650;

    /// <summary>Weight of the last 30 days' rate in the blended rate; the 90-day rate gets the rest.</summary>
    public const decimal RecentWeight = 0.6m;

    /// <summary>The same rate every day.</summary>
    /// <param name="perDay">Units per day.</param>
    /// <returns>The forecast.</returns>
    public static DemandForecast Flat(decimal perDay) => new(perDay, SeasonalityIndex.Flat);

    /// <summary>
    /// Blends recent and longer sales into a season-free base rate: each rate is divided by the season factor
    /// of the days it was measured over (a busy May inflates May's rate), then the last 30 days get
    /// <see cref="RecentWeight"/> and the 90-day rate the rest, so a trend shows quickly without one slow or
    /// busy month taking over.
    /// </summary>
    /// <param name="today">Today (the last day measured).</param>
    /// <param name="rate90">Units per day over the 90-day window (the planning velocity).</param>
    /// <param name="days90">Days the 90-day rate covers (first sale in the window to today).</param>
    /// <param name="units30">Units sold in the last 30 days.</param>
    /// <param name="days30">Days the 30-day figure covers (at most 30; fewer when sales started later).</param>
    /// <param name="season">The season index.</param>
    /// <returns>The forecast, or <see langword="null"/> without a usable 90-day rate.</returns>
    public static DemandForecast? Blend(DateOnly today, decimal? rate90, int days90, int units30, int days30, SeasonalityIndex season)
    {
        ArgumentNullException.ThrowIfNull(season);
        if (rate90 is not { } longer || longer <= 0 || days90 <= 0)
        {
            return null;
        }

        var base90 = longer / season.Average(today.AddDays(1 - days90), days90);
        if (days30 <= 0)
        {
            return new DemandForecast(base90, season);
        }

        var base30 = units30 / (decimal)days30 / season.Average(today.AddDays(1 - days30), days30);
        return new DemandForecast((RecentWeight * base30) + ((1 - RecentWeight) * base90), season);
    }

    /// <summary>Expected units sold on a day.</summary>
    /// <param name="day">The day.</param>
    /// <returns>Units.</returns>
    public decimal On(DateOnly day) => BaseRate * Season.On(day);

    /// <summary>Expected units sold over a run of days.</summary>
    /// <param name="from">First day.</param>
    /// <param name="days">How many days.</param>
    /// <returns>Units (not rounded).</returns>
    public decimal Units(DateOnly from, int days)
    {
        var total = 0m;
        for (var i = 0; i < days; i++)
        {
            total += On(from.AddDays(i));
        }

        return total;
    }

    /// <summary>Whole days a stock lasts from a day on, at the expected sales.</summary>
    /// <param name="from">First day it sells on.</param>
    /// <param name="units">Units in stock.</param>
    /// <returns>Days until it runs out (at most <see cref="MaxDays"/>).</returns>
    public int DaysCovered(DateOnly from, decimal units)
    {
        var left = units;
        for (var day = 0; day < MaxDays; day++)
        {
            left -= On(from.AddDays(day));
            if (left < 0)
            {
                return day;
            }
        }

        return MaxDays;
    }
}
