using AERai.Web.Application.Profit;

namespace AERai.Web.Application.Tests.Profit;

/// <summary>Splitting to the cent by weight.</summary>
public sealed class ProfitAllocatorTests
{
    [Fact]
    public void Split_ThreeWays_PartsAddUpToTheCent()
    {
        var parts = ProfitAllocator.Split(-10m, [1m, 1m, 1m]);

        Assert.Equal([-3.34m, -3.33m, -3.33m], parts);
    }

    [Fact]
    public void Split_ZeroWeights_GetNothing()
    {
        Assert.Equal([0m, 5m], ProfitAllocator.Split(5m, [0m, 2m]));
    }

    [Fact]
    public void Split_NoPositiveWeight_IsNull()
    {
        Assert.Null(ProfitAllocator.Split(5m, [0m, -1m]));
        Assert.Null(ProfitAllocator.Split(5m, []));
    }
}
