using AERai.Web.Application.Products;
using AERai.Web.Application.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Products;

public sealed class ProductServiceTests
{
    private readonly FakeProductRepository _repository = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    public ProductServiceTests()
    {
        _repository.Products["A-1"] = new ProductSummary("A-1", null, "Mat", null, DateTimeOffset.MinValue);
    }

    private ProductService CreateService() => new(_repository, _clock, NullLogger<ProductService>.Instance);

    [Fact]
    public async Task UpdateCostAsync_ValidCost_RoundsToCentsAndStampsTime()
    {
        var result = await CreateService().UpdateCostAsync("A-1", 4.125m, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4.13m, _repository.Products["A-1"].CostOfGoods);
        Assert.Equal(_clock.GetUtcNow(), _repository.Products["A-1"].UpdatedAt);
    }

    [Fact]
    public async Task UpdateCostAsync_NullCost_ClearsIt()
    {
        var result = await CreateService().UpdateCostAsync("A-1", null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(_repository.Products["A-1"].CostOfGoods);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100_000.01)]
    public async Task UpdateCostAsync_OutOfRange_Fails(double cost)
    {
        var result = await CreateService().UpdateCostAsync("A-1", (decimal)cost, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpdateCostAsync_UnknownSku_Fails()
    {
        var result = await CreateService().UpdateCostAsync("NOPE", 1m, CancellationToken.None);

        Assert.True(result.IsFailure);
    }
}
