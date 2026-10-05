using AERai.Web.Domain.Ingestion;
using AERai.Web.Infrastructure.SpApi;

namespace AERai.Web.Infrastructure.Tests.SpApi;

/// <summary>
/// Pins the SP-API report type codes. Several Amazon report types have near-identical names but
/// different layouts, so a wrong code fails only against live data — these tests catch it earlier.
/// </summary>
public sealed class SpApiReportTypesTests
{
    [Theory]
    [InlineData(AmazonReportType.Orders, "GET_FLAT_FILE_ALL_ORDERS_DATA_BY_LAST_UPDATE_GENERAL")]
    [InlineData(AmazonReportType.FbaInventory, "GET_FBA_MYI_ALL_INVENTORY_DATA")]
    [InlineData(AmazonReportType.Settlements, "GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2")]
    public void ToCode_ReturnsTheReportWhoseLayoutStagingExpects(AmazonReportType type, string expected) =>
        Assert.Equal(expected, SpApiReportTypes.ToCode(type));
}
