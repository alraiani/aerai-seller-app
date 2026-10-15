using AERai.Web.Application.Products;

namespace AERai.Web.Application.Tests.Products;

public sealed class ProductCostTextTests
{
    [Theory]
    [InlineData("4.5", 4.50)]
    [InlineData(" $1,204.50 ", 1204.50)]
    [InlineData("CA$ 3", 3)]
    [InlineData("£2.105", 2.11)]
    [InlineData("0", 0)]
    public void TryParse_NumbersWithSymbols_ParsesAndRoundsToCents(string text, double expected)
    {
        Assert.True(ProductCostText.TryParse(text, out var cost));
        Assert.Equal((decimal)expected, cost);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryParse_Blank_IsNoCost(string? text)
    {
        Assert.True(ProductCostText.TryParse(text, out var cost));
        Assert.Null(cost);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("100000.01")]
    [InlineData("1.2.3")]
    public void TryParse_NotACost_Fails(string text) => Assert.False(ProductCostText.TryParse(text, out _));

    [Fact]
    public void Format_RoundTripsThroughTryParse()
    {
        Assert.Equal("4.50", ProductCostText.Format(4.5m));
        Assert.Equal(string.Empty, ProductCostText.Format(null));
        Assert.True(ProductCostText.TryParse(ProductCostText.Format(1234.5m), out var cost));
        Assert.Equal(1234.50m, cost);
    }
}
