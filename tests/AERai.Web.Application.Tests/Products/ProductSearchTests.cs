using AERai.Web.Application.Products;

namespace AERai.Web.Application.Tests.Products;

public sealed class ProductSearchTests
{
    [Fact]
    public void Terms_SplitsOnWhitespaceDropsRepeatsAndCaps()
    {
        Assert.Equal(["mat", "blue"], ProductSearch.Terms("  mat   blue MAT "));
        Assert.Equal(ProductSearch.MaxTerms, ProductSearch.Terms("a b c d e f g").Count);
        Assert.Empty(ProductSearch.Terms(" "));
        Assert.Empty(ProductSearch.Terms(null));
    }

    [Theory]
    [InlineData(ProductSort.Sku, false)]
    [InlineData(ProductSort.Title, false)]
    [InlineData(ProductSort.Family, false)]
    [InlineData(ProductSort.Cost, true)]
    [InlineData(ProductSort.Updated, true)]
    public void IsDescendingByDefault_HighestAndNewestFirst(ProductSort sort, bool expected) =>
        Assert.Equal(expected, sort.IsDescendingByDefault());
}
