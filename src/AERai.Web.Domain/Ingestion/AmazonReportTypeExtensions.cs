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
    /// <returns><see langword="false"/> for <see cref="AmazonReportType.AwdInventory"/> and <see cref="AmazonReportType.OrdersApi"/>.</returns>
    public static bool IsReport(this AmazonReportType reportType) =>
        reportType is not (AmazonReportType.AwdInventory or AmazonReportType.OrdersApi);

    /// <summary>
    /// The shortest time Amazon allows between two requests for the report in one marketplace. Amazon
    /// accepts a request made sooner but then fails it as <c>FATAL</c>, so the request is held back instead.
    /// </summary>
    /// <param name="reportType">The report type.</param>
    /// <returns>30 minutes for <see cref="AmazonReportType.FbaInventory"/>; <see cref="TimeSpan.Zero"/> (no limit) otherwise.</returns>
    public static TimeSpan RequestCooldown(this AmazonReportType reportType) =>
        reportType == AmazonReportType.FbaInventory ? TimeSpan.FromMinutes(30) : TimeSpan.Zero;
}
