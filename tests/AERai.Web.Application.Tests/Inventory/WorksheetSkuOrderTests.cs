using AERai.Web.Application.Inventory;

namespace AERai.Web.Application.Tests.Inventory;

public sealed class WorksheetSkuOrderTests
{
    [Theory]
    [InlineData("FOB00BL", "FOB")]
    [InlineData("SBP001BL", "SBP")]
    [InlineData("HK-WHT", "HK-WHT")]
    [InlineData("1234", "1234")]
    public void Style_IsTheLettersBeforeTheFirstDigit(string sku, string expected) =>
        Assert.Equal(expected, WorksheetSkuOrder.Style(sku));

    [Fact]
    public void Order_PutsOneOffStylesAfterSharedOnes()
    {
        var family = new[] { ("TOJ00BL", 1), ("SBP001BL", 1), ("FOB00BL", 1), ("FOB00GR", 2), ("TOJ00GR", 2) };
        var shared = WorksheetSkuOrder.SharedStyles(family);

        var blue = WorksheetSkuOrder.Order(family.Where(f => f.Item2 == 1).Select(f => f.Item1), s => s, shared);

        Assert.Equal(["FOB00BL", "TOJ00BL", "SBP001BL"], blue);
    }

    [Fact]
    public void Order_OneColorFamily_IsPlainAToZ()
    {
        var family = new[] { ("B2", 1), ("A1", 1) };

        Assert.Equal(["A1", "B2"], WorksheetSkuOrder.Order(family.Select(f => f.Item1), s => s, WorksheetSkuOrder.SharedStyles(family)));
    }
}
