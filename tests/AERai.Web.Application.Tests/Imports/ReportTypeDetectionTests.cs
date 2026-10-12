using System.Text;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Core;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Imports;

public sealed class ReportTypeDetectionTests
{
    private const string OrdersHeader = "amazon-order-id\tpurchase-date\torder-status\tsku\tquantity\titem-price\n";

    // The columns of a real Seller Central settlement download (V2 flat file).
    private const string SettlementHeader =
        "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\ttransaction-type\torder-id\tmerchant-order-id\tadjustment-id\tshipment-id\t" +
        "marketplace-name\tamount-type\tamount-description\tamount\tfulfillment-id\tposted-date\tposted-date-time\torder-item-code\tmerchant-order-item-id\tmerchant-adjustment-item-id\t" +
        "sku\tquantity-purchased\tpromotion-id\n";

    private readonly FakeRawFileStore _rawFiles = new();
    private readonly FakeStagingRepository _staged = new();

    private StagingImportService CreateService() => new(
        [new OrderLineMapper(), new SettlementLineMapper(), new InventoryRowMapper(), new FbaInventoryRowMapper(), new FbaReservedInventoryRowMapper(), new FbaRestockRowMapper()],
        _rawFiles, _staged, new FakeImportBatchQueries(), Options.Create(new ImportOptions()), new FakeTimeProvider(new DateTimeOffset(2026, 10, 11, 12, 0, 0, TimeSpan.Zero)),
        NullLogger<StagingImportService>.Instance);

    private static MemoryStream Text(string content) => new(Encoding.UTF8.GetBytes(content));

    [Theory]
    [InlineData(OrdersHeader, ImportSource.Orders)]
    [InlineData(SettlementHeader, ImportSource.Settlements)]
    [InlineData("snapshot-date\tsku\tstate\tquantity\n", ImportSource.Inventory)]
    [InlineData("Amazon Order Id,Purchase Date,Order Status,SKU,Quantity,Item Price\n", ImportSource.Orders)] // Title-case, comma-separated.
    public async Task DetectSourceAsync_RecognisesTheReportFromItsColumns(string header, ImportSource expected)
    {
        var result = await CreateService().DetectSourceAsync(Text(header + "x\n"), CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public async Task DetectSourceAsync_ColumnsFitNothing_FailsAndSaysSo()
    {
        var result = await CreateService().DetectSourceAsync(Text("colour\tsize\nred\tL\n"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("don't match any report type", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DetectSourceAsync_EmptyFile_Fails() =>
        Assert.True((await CreateService().DetectSourceAsync(Text(string.Empty), CancellationToken.None)).IsFailure);

    [Fact]
    public async Task ImportAsync_SettlementFileUploadedAsOrders_SaysItLooksLikeASettlementFile()
    {
        var bytes = Encoding.UTF8.GetBytes(SettlementHeader + "25289908211\t\t\t\t\t\tOrder\t111-1\t\t\t\tAmazon.com\tItemPrice\tPrincipal\t18.64\tAFN\t2025-12-29\t\t\t\t\tHH1006\t1\t\n");

        var result = await CreateService().ImportAsync(
            new ImportFileCommand(ImportSource.Orders, MarketplaceIds.UnitedStates, "2025-12-14 to 2026-01-11.txt", bytes.Length, new MemoryStream(bytes), "tests"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains("missing required Orders column(s)", result.Error, StringComparison.Ordinal);
        Assert.Contains("It looks like a Settlements file: choose that report type.", result.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ImportAsync_FileThatFitsNoOtherReport_GetsNoGuess()
    {
        var bytes = Encoding.UTF8.GetBytes("amazon-order-id\tsku\n111-1\tA-1\n");

        var result = await CreateService().ImportAsync(
            new ImportFileCommand(ImportSource.Orders, MarketplaceIds.UnitedStates, "partial.tsv", bytes.Length, new MemoryStream(bytes), "tests"), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.DoesNotContain("looks like", result.Error, StringComparison.Ordinal);
    }
}
