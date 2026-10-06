namespace AERai.Web.Domain.Ingestion;

/// <summary>Facts about <see cref="AmazonReportType"/> values that several layers rely on.</summary>
public static class AmazonReportTypeExtensions
{
    /// <summary>
    /// Whether the report is a point-in-time snapshot of current state, which Amazon cannot produce
    /// for a past date (so it has no history to backfill).
    /// </summary>
    /// <param name="reportType">The report type.</param>
    /// <returns><see langword="true"/> for inventory snapshots.</returns>
    public static bool IsSnapshot(this AmazonReportType reportType) =>
        reportType is AmazonReportType.FbaInventory or AmazonReportType.FbaReservedInventory;
}
