using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class DemandForecastTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);

    [Fact]
    public void Blend_WeighsTheLast30DaysMoreThan90()
    {
        // 90-day rate 1/day, but the last 30 days sold 3/day: 0.6 × 3 + 0.4 × 1.
        var forecast = DemandForecast.Blend(Today, 1m, 90, 90, 30, SeasonalityIndex.Flat);

        Assert.Equal(2.2m, forecast!.BaseRate);
    }

    [Fact]
    public void Blend_WithoutA90DayRate_IsUnknown() =>
        Assert.Null(DemandForecast.Blend(Today, null, 90, 30, 30, SeasonalityIndex.Flat));

    [Fact]
    public void DaysCovered_MatchesAFlatRate()
    {
        var flat = DemandForecast.Flat(10m);

        Assert.Equal((10, 0, 9), (flat.DaysCovered(Today, 100), flat.DaysCovered(Today, 0), flat.DaysCovered(Today, 99)));
        Assert.Equal(300m, flat.Units(Today, 30));
    }
}
