using AERai.Web.Application.Products;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Products;

public sealed class ProductServiceTests
{
    private const string Us = MarketplaceIds.UnitedStates;

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
        var result = await CreateService().UpdateCostAsync("A-1", Us, 4.125m, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal((4.13m, _clock.GetUtcNow()), _repository.Costs[("A-1", Us)]);
    }

    [Fact]
    public async Task UpdateCostAsync_NullCost_ClearsIt()
    {
        var result = await CreateService().UpdateCostAsync("A-1", Us, null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(_repository.Costs[("A-1", Us)].Cost);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100_000.01)]
    public async Task UpdateCostAsync_OutOfRange_Fails(double cost)
    {
        var result = await CreateService().UpdateCostAsync("A-1", Us, (decimal)cost, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpdateCostAsync_UnknownSku_Fails()
    {
        var result = await CreateService().UpdateCostAsync("NOPE", Us, 1m, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task UpdateCostAsync_OneMarketplace_LeavesOtherMarketplacesUntouched()
    {
        var service = CreateService();
        await service.UpdateCostAsync("A-1", Us, 4m, CancellationToken.None);

        await service.UpdateCostAsync("A-1", MarketplaceIds.Canada, 5.5m, CancellationToken.None);

        Assert.Equal(4m, _repository.Costs[("A-1", Us)].Cost);
        Assert.Equal(5.5m, _repository.Costs[("A-1", MarketplaceIds.Canada)].Cost);
    }
}
