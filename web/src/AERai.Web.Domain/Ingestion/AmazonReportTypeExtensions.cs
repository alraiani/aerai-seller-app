namespace AERai.Web.Domain.Ingestion;

/// <summary>Facts about <see cref="AmazonReportType"/> values that several layers rely on.</summary>
public static class AmazonReportTypeExtensions
{
    /// <summary>
    /// Whether the report is a point-in-time snapshot of current state, which Amazon cannot produce
    /// for a past date (so it has no history to backfill).
    /// </summary>
    /// <param name="reportType">The report type.</param>
    /// <returns><see langword="true"/> for inventory snapshots (FBA and AWD) and restock recommendations.</returns>
    public static bool IsSnapshot(this AmazonReportType reportType) =>
        reportType is AmazonReportType.FbaInventory or AmazonReportType.FbaReservedInventory or AmazonReportType.RestockRecommendations
            or AmazonReportType.AwdInventory;

    /// <summary>
    /// Whether the data comes from the Reports API (a document that is landed, staged, and promoted)
    /// rather than from a JSON API written straight to <c>core</c>.
    /// </summary>
    /// <param name="reportType">The report type.</param>
    /// <returns><see langword="false"/> for <see cref="AmazonReportType.AwdInventory"/>.</returns>
    public static bool IsReport(this AmazonReportType reportType) => reportType != AmazonReportType.AwdInventory;
}
