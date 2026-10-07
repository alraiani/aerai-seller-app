using System.Text;
using AERai.Web.Application.Imports;
using AERai.Web.Application.Tests.Fakes;
using AERai.Web.Domain.Staging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace AERai.Web.Application.Tests.Imports;

/// <summary>
/// Guards the settlement mapper against Amazon's two similarly named settlement layouts, using
/// the exact header rows Amazon publishes for each.
/// </summary>
public sealed class SettlementLayoutTests
{
    /// <summary>GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2 — the long layout the app requests.</summary>
    private const string FlatFileV2Header =
        "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\ttransaction-type\torder-id\t" +
        "merchant-order-id\tadjustment-id\tshipment-id\tmarketplace-name\tamount-type\tamount-description\tamount\tfulfillment-id\t" +
        "posted-date\tposted-date-time\torder-item-code\tmerchant-order-item-id\tmerchant-adjustment-item-id\tsku\tquantity-purchased\tpromotion-id";

    /// <summary>GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE — the wide layout (one column per kind of amount).</summary>
    private const string FlatFileWideHeader =
        "settlement-id\tsettlement-start-date\tsettlement-end-date\tdeposit-date\ttotal-amount\tcurrency\ttransaction-type\torder-id\t" +
        "merchant-order-id\tadjustment-id\tshipment-id\tmarketplace-name\tshipment-fee-type\tshipment-fee-amount\torder-fee-type\t" +
        "order-fee-amount\tfulfillment-id\tposted-date\torder-item-code\tmerchant-order-item-id\tmerchant-adjustment-item-id\tsku\t" +
        "quantity-purchased\tprice-type\tprice-amount\titem-related-fee-type\titem-related-fee-amount\tmisc-fee-amount\t" +
        "other-fee-amount\tother-fee-reason-description\tpromotion-id\tpromotion-type\tpromotion-amount\tdirect-payment-type\t" +
        "direct-payment-amount\tother-amount";

    private readonly FakeStagingRepository _staged = new();

    private Task<AERai.Web.Application.Common.Result<ImportReceipt>> ImportAsync(string content)
    {
        var service = new StagingImportService(
            [new SettlementLineMapper()], new FakeRawFileStore(), _staged, new FakeImportBatchQueries(),
            Options.Create(new ImportOptions()), new FakeTimeProvider(), NullLogger<StagingImportService>.Instance);
        var bytes = Encoding.UTF8.GetBytes(content);
        return service.ImportAsync(new ImportFileCommand(ImportSource.Settlements, "ATVPDKIKX0DER", "s.tsv", bytes.Length, new MemoryStream(bytes), "t"), CancellationToken.None);
    }

    [Fact]
    public async Task FlatFileV2_StagesAmountTypeDescriptionAndAmount()
    {
        var result = await ImportAsync(FlatFileV2Header + "\n" +
            "123\t\t\t\t\t\tOrder\t111-1\t\t\t\tAmazon.com\tItemFees\tCommission\t-4.50\tAFN\t2026-09-10\t2026-09-10T12:00:00+00:00\t\t\t\tSKU-1\t1\t\n");

        Assert.True(result.IsSuccess, result.Error);
        var line = Assert.Single(Assert.Single(_staged.Saved).SettlementLines);
        Assert.Equal(("ItemFees", "Commission", "-4.50"), (line.AmountType, line.AmountDescription, line.Amount));
    }

    [Fact]
    public async Task WideFlatFile_IsRejectedWithMissingAmountColumn()
    {
        var result = await ImportAsync(FlatFileWideHeader + "\n123\t\t\t\t\t\tOrder\n");

        Assert.True(result.IsFailure);
        Assert.Contains("amount", result.Error, StringComparison.Ordinal);
    }
}
