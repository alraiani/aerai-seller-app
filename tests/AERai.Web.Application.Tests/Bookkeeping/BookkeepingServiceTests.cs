using AERai.Web.Application.Bookkeeping;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Bookkeeping;

public sealed class BookkeepingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeBookkeeping _data = new();
    private readonly BookkeepingService _service;

    public BookkeepingServiceTests()
    {
        _service = new BookkeepingService(_data, _data, new FakeTimeProvider(Now), NullLogger<BookkeepingService>.Instance);
        _data.Settlements["S1"] = new SettlementBooks(
            "S1", MarketplaceIds.UnitedStates, "USD", new DateTimeOffset(2026, 9, 14, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 28, 0, 0, 0, TimeSpan.Zero),
            [new SettlementCategoryTotal("ItemPrice", "Principal", 100m, 3), new SettlementCategoryTotal("ItemFees", "Commission", -15m, 3)]);
    }

    private async Task MapEverythingAsync()
    {
        _data.DepositAccounts[MarketplaceIds.UnitedStates] = "Checking";
        await _service.SaveMappingsAsync(
            MarketplaceIds.UnitedStates, "Checking",
            [new AccountMappingInput("ItemPrice", "Principal", "Sales"), new AccountMappingInput("ItemFees", "Commission", "Fees")],
            "ops@example.com", CancellationToken.None);
    }

    [Fact]
    public async Task ExportAsync_FullyMapped_StoresTheFileAndReturnsIt()
    {
        await MapEverythingAsync();

        var result = await _service.ExportAsync(MarketplaceIds.UnitedStates, "S1", "ops@example.com", CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        var export = Assert.Single(_data.Exports);
        Assert.Equal("Amazon-Settlement-S1-20261011120000.iif", export.FileName);
        Assert.Equal((85m, "USD", "Checking", "ops@example.com"), (export.NetTotal, export.Currency, export.DepositAccountName, export.GeneratedBy));
        Assert.Contains("TRNS\t1\tGENERAL JOURNAL\t09/28/2026\tChecking\t\t\t85.00\tS1", export.Content, StringComparison.Ordinal);
        Assert.Equal(export.Id, result.Value.Id);
    }

    [Fact]
    public async Task ExportAsync_UnmappedAmounts_FailsAndStoresNothing()
    {
        _data.DepositAccounts[MarketplaceIds.UnitedStates] = "Checking";

        var result = await _service.ExportAsync(MarketplaceIds.UnitedStates, "S1", "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("2 unmapped kinds", result.Error, StringComparison.Ordinal);
        Assert.Empty(_data.Exports);
    }

    [Fact]
    public async Task ExportAsync_NoDepositAccount_Fails()
    {
        await MapEverythingAsync();
        _data.DepositAccounts.Clear();

        var result = await _service.ExportAsync(MarketplaceIds.UnitedStates, "S1", "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("deposit account", result.Error, StringComparison.Ordinal);
        Assert.Empty(_data.Exports);
    }

    [Fact]
    public async Task ExportAsync_SettlementInAnotherMarketplace_IsNotFound()
    {
        await MapEverythingAsync();

        var result = await _service.ExportAsync(MarketplaceIds.Canada, "S1", "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("not found", result.Error, StringComparison.Ordinal);
        Assert.Empty(_data.Exports);
    }

    [Fact]
    public async Task ExportAsync_SettlementWithNoLines_Fails()
    {
        _data.Settlements["EMPTY"] = new SettlementBooks("EMPTY", MarketplaceIds.UnitedStates, "USD", null, null, []);

        var result = await _service.ExportAsync(MarketplaceIds.UnitedStates, "EMPTY", "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("no lines", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetExportFileAsync_OtherMarketplace_ReturnsNull()
    {
        await MapEverythingAsync();
        var exported = await _service.ExportAsync(MarketplaceIds.UnitedStates, "S1", "ops@example.com", CancellationToken.None);

        Assert.NotNull(await _service.GetExportFileAsync(MarketplaceIds.UnitedStates, exported.Value.Id, CancellationToken.None));
        Assert.Null(await _service.GetExportFileAsync(MarketplaceIds.Canada, exported.Value.Id, CancellationToken.None));
    }

    [Fact]
    public async Task GetMappingViewAsync_ListsUnmappedKindsFirst()
    {
        _data.Mappings.Add(new BookkeepingAccountMapping { AmountType = "ItemFees", AmountDescription = "Commission", QuickBooksAccountName = "Fees", UpdatedBy = "t" });

        var view = await _service.GetMappingViewAsync(MarketplaceIds.UnitedStates, CancellationToken.None);

        Assert.Equal(1, view.UnmappedCount);
        Assert.Equal(["ItemPrice/Principal", "ItemFees/Commission"], view.Rows.Select(r => $"{r.AmountType}/{r.AmountDescription}"));
        Assert.Null(view.DepositAccountName);
    }

    [Fact]
    public async Task SaveMappingsAsync_TrimsNamesAndBlankRemovesAMapping()
    {
        await _service.SaveMappingsAsync(
            MarketplaceIds.UnitedStates, "  Checking  ",
            [new AccountMappingInput("ItemPrice", "Principal", "  Sales  "), new AccountMappingInput("ItemFees", "Commission", "Fees")],
            "ops@example.com", CancellationToken.None);
        await _service.SaveMappingsAsync(
            MarketplaceIds.UnitedStates, "Checking", [new AccountMappingInput("ItemFees", "Commission", " ")], "ops@example.com", CancellationToken.None);

        var mapping = Assert.Single(_data.Mappings);
        Assert.Equal(("ItemPrice", "Sales"), (mapping.AmountType, mapping.QuickBooksAccountName));
        Assert.Equal("Checking", _data.DepositAccounts[MarketplaceIds.UnitedStates]);
    }

    [Theory]
    [InlineData("Bad\tName")]
    [InlineData("Bad\nName")]
    public async Task SaveMappingsAsync_ControlCharacterInAName_IsRejectedAndNothingIsSaved(string name)
    {
        var result = await _service.SaveMappingsAsync(
            MarketplaceIds.UnitedStates, "Checking", [new AccountMappingInput("ItemPrice", "Principal", name)], "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_data.Mappings);
        Assert.Empty(_data.DepositAccounts);
    }

    [Fact]
    public async Task SaveMappingsAsync_ControlCharacterInTheDepositAccount_IsRejected()
    {
        var result = await _service.SaveMappingsAsync(MarketplaceIds.UnitedStates, "Bank\tOne", [], "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_data.DepositAccounts);
    }

    [Fact]
    public async Task SaveMappingsAsync_NameOverTheLimit_IsRejected()
    {
        var result = await _service.SaveMappingsAsync(
            MarketplaceIds.UnitedStates, null, [new AccountMappingInput("ItemPrice", "Principal", new string('x', BookkeepingService.MaxAccountNameLength + 1))],
            "ops@example.com", CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(_data.Mappings);
    }
}
