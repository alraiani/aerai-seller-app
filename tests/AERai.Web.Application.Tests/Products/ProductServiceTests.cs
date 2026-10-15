using System.Text;
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
    private readonly FakeSpreadsheetReader _spreadsheets = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));

    public ProductServiceTests()
    {
        _repository.Products["A-1"] = new ProductSummary("A-1", null, "Mat", null, DateTimeOffset.MinValue);
        _repository.Products["B-2"] = new ProductSummary("B-2", null, "Strap", null, DateTimeOffset.MinValue);
        _repository.Products["C-3"] = new ProductSummary("C-3", null, "Block", null, DateTimeOffset.MinValue);
    }

    private ProductService CreateService() => new(_repository, _spreadsheets, _clock, NullLogger<ProductService>.Instance);

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

    [Fact]
    public async Task SaveCostsAsync_RoundsDropsNoOpsAndKeepsTheLastChangePerSku()
    {
        _repository.Costs[("B-2", Us)] = (2m, DateTimeOffset.MinValue);

        var result = await CreateService().SaveCostsAsync(
            Us,
            [new("A-1", null, 1m), new("A-1", null, 4.125m), new("B-2", 2m, 2.001m), new("C-3", null, null)],
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, result.Value.Applied);
        Assert.Equal([("A-1", (decimal?)null, (decimal?)4.13m)], Assert.Single(_repository.AppliedBatches).Select(c => (c.Sku, c.Current, c.New)));
        Assert.Equal((4.13m, _clock.GetUtcNow()), _repository.Costs[("A-1", Us)]);
    }

    [Fact]
    public async Task SaveCostsAsync_CostChangedSinceRead_IsReportedStaleNotOverwritten()
    {
        _repository.Costs[("A-1", Us)] = (3m, DateTimeOffset.MinValue);

        var result = await CreateService().SaveCostsAsync(Us, [new("A-1", 2m, 5m), new("B-2", null, 1m)], CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(1, result.Value.Applied);
        Assert.Equal(["A-1"], result.Value.Stale);
        Assert.Equal(3m, _repository.Costs[("A-1", Us)].Cost);
    }

    [Fact]
    public async Task SaveCostsAsync_OneOutOfRange_FailsAndSavesNothing()
    {
        var result = await CreateService().SaveCostsAsync(Us, [new("A-1", null, 1m), new("B-2", null, 100_000.01m)], CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("B-2", result.Error, StringComparison.Ordinal);
        Assert.Empty(_repository.AppliedBatches);
    }

    [Fact]
    public async Task SaveCostsAsync_NothingChanged_DoesNotWrite()
    {
        var result = await CreateService().SaveCostsAsync(Us, [new("A-1", null, null)], CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Applied);
        Assert.Empty(_repository.AppliedBatches);
    }

    [Fact]
    public async Task PreviewCostImportAsync_ComparesWithCostsAndSavesNothing()
    {
        _repository.Costs[("B-2", Us)] = (2m, DateTimeOffset.MinValue);
        _repository.Costs[("C-3", Us)] = (7.5m, DateTimeOffset.MinValue);
        const string csv =
            "SKU,Cost of Goods,ASIN\n" +
            "A-1,$4.50,B0\n" +
            "B-2,2.00,B0\n" +
            "GHOST,1,B0\n" +
            "C-3,abc,B0\n" +
            ",3,B0\n" +
            "C-3,,B0\n" +
            "C-3,\"1,204.5\",B0\n";

        var result = await CreateService().PreviewCostImportAsync(Us, "cogs.csv", Text(csv), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var preview = result.Value;
        Assert.Equal([("A-1", (decimal?)null, (decimal?)4.50m), ("C-3", 7.5m, 1204.50m)], preview.Changes.Select(c => (c.Sku, c.Current, c.New)));
        Assert.Equal((1, 1, 1, 1), (preview.NewCount, preview.UpdatedCount, preview.UnchangedCount, preview.BlankCount));
        Assert.Equal([3, 4, 5], preview.Rejected.Select(r => r.RowNumber));
        Assert.Empty(_repository.AppliedBatches);
        Assert.False(_spreadsheets.WasCalled);
    }

    [Fact]
    public async Task PreviewCostImportAsync_MissingColumn_Fails()
    {
        var result = await CreateService().PreviewCostImportAsync(Us, "cogs.csv", Text("sku,cost\nA-1,1\n"), CancellationToken.None);

        Assert.Contains("'cost-of-goods'", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PreviewCostImportAsync_UnsupportedType_Fails()
    {
        var result = await CreateService().PreviewCostImportAsync(Us, "cogs.pdf", Text("x"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    private static MemoryStream Text(string text) => new(Encoding.UTF8.GetBytes(text));
}
