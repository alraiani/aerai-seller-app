using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>Maps the app's report types to SP-API report type codes.</summary>
internal static class SpApiReportTypes
{
    /// <summary>Gets the SP-API code for a report type.</summary>
    /// <param name="reportType">App report type.</param>
    /// <returns>The SP-API <c>reportType</c> value.</returns>
    public static string ToCode(AmazonReportType reportType) => reportType switch
    {
        AmazonReportType.Orders => "GET_FLAT_FILE_ALL_ORDERS_DATA_BY_LAST_UPDATE_GENERAL",
        AmazonReportType.FbaInventory => "GET_FBA_MYI_ALL_INVENTORY_DATA",
        AmazonReportType.FbaReservedInventory => "GET_RESERVED_INVENTORY_DATA",
        AmazonReportType.RestockRecommendations => "GET_RESTOCK_INVENTORY_RECOMMENDATIONS_REPORT",
        // The _V2 flat file is the "long" layout (one row per amount with amount-type/amount-description/
        // amount) that stg.SettlementLine mirrors. The similarly named GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE
        // is a "wide" layout (price-amount, item-related-fee-amount, ...) and fails staging. Amazon
        // generates both for every settlement, so requesting _V2 loses nothing.
        AmazonReportType.Settlements => "GET_V2_SETTLEMENT_REPORT_DATA_FLAT_FILE_V2",
        _ => throw new ArgumentOutOfRangeException(nameof(reportType), reportType, "Unsupported report type."),
    };
}
