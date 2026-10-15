using AERai.Web.Application.Products;

namespace AERai.Web.Application.Tests.Products;

public sealed class ProductCostPayloadTests
{
    [Fact]
    public void WriteThenRead_KeepsSkusAndCostsIncludingUnset()
    {
        ProductCostChange[] changes = [new("A-1", null, 4.5m), new("B-2", 2m, 3.25m)];

        var read = ProductCostPayload.Read(ProductCostPayload.Write(changes));

        Assert.True(read.IsSuccess, read.Error);
        Assert.Equal(changes.Select(c => (c.Sku, c.Current, c.New)), read.Value.Select(c => (c.Sku, c.Current, c.New)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A-1\t1")]
    [InlineData("A-1\tx\t2")]
    [InlineData("\t1\t2")]
    public void Read_EmptyOrMalformed_Fails(string text) => Assert.True(ProductCostPayload.Read(text).IsFailure);
}
