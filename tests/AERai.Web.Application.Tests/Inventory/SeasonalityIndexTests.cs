using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class SeasonalityIndexTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    // A wedding year: May–September sell three times what November–February do.
    private static IEnumerable<(int Year, int Month, int Units)> WeddingYears(int years)
    {
        int[] pattern = [10, 10, 20, 30, 30, 30, 30, 30, 30, 20, 10, 10];
        for (var m = 0; m < years * 12; m++)
        {
            var date = new DateOnly(2026, 10, 1).AddMonths(-(years * 12) + m);
            yield return (date.Year, date.Month, pattern[date.Month - 1]);
        }
    }

    [Fact]
    public void From_LearnsBusyAndQuietMonths()
    {
        var index = SeasonalityIndex.From(WeddingYears(2), Today);

        Assert.True(index.IsLearned);
        Assert.True(index.ForMonth(6) > 1.3m, $"June {index.ForMonth(6)}");
        Assert.True(index.ForMonth(1) < 0.6m, $"January {index.ForMonth(1)}");
    }

    [Fact]
    public void From_LessThanAYearOfHistory_IsFlat()
    {
        var index = SeasonalityIndex.From(WeddingYears(2).TakeLast(11), Today);

        Assert.False(index.IsLearned);
        Assert.All(Enumerable.Range(1, 12), m => Assert.Equal(1m, index.ForMonth(m)));
    }

    [Fact]
    public void From_IgnoresTheRunningMonthAndClampsExtremes()
    {
        var withFreak = WeddingYears(2).Append((2026, 10, 100_000)).ToList();
        var oneHugeMonth = WeddingYears(2).Select(m => m.Month == 7 ? (m.Year, m.Month, 10_000) : m);

        Assert.Equal(SeasonalityIndex.From(WeddingYears(2), Today).ForMonth(10), SeasonalityIndex.From(withFreak, Today).ForMonth(10));
        Assert.Equal(SeasonalityIndex.MaxFactor, SeasonalityIndex.From(oneHugeMonth, Today).ForMonth(7));
    }
}
