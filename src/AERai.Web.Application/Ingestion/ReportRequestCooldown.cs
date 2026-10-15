using System.Globalization;
using AERai.Web.Domain.Ingestion;

namespace AERai.Web.Application.Ingestion;

/// <summary>
/// When a report held back by its <see cref="AmazonReportTypeExtensions.RequestCooldown"/> may next be
/// requested, and how that wait is explained to the user. The run itself and the Run now / Sync all
/// feedback both use it, so they always agree.
/// </summary>
public static class ReportRequestCooldown
{
    /// <summary>Added to Amazon's limit to cover clock differences between us and Amazon.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromMinutes(1);

    /// <summary>
    /// When the report may next be requested, if that is still in the future.
    /// </summary>
    /// <param name="reportType">The report type.</param>
    /// <param name="lastRequest">The last request for this report type and marketplace (by any run), if any.</param>
    /// <param name="now">The current time.</param>
    /// <returns>
    /// The last request plus the cooldown and <see cref="Margin"/>; <see langword="null"/> when the report
    /// has no cooldown, was never requested, or may be requested now.
    /// </returns>
    public static DateTimeOffset? WaitUntil(AmazonReportType reportType, DateTimeOffset? lastRequest, DateTimeOffset now)
    {
        var cooldown = reportType.RequestCooldown();
        if (cooldown <= TimeSpan.Zero || lastRequest is not { } last)
        {
            return null;
        }

        var allowedAt = last + cooldown + Margin;
        return allowedAt > now ? allowedAt : null;
    }

    /// <summary>
    /// The sentence shown while a run waits: until when, and why.
    /// </summary>
    /// <param name="reportType">The report type.</param>
    /// <param name="waitUntil">When the request will be sent (see <see cref="WaitUntil"/>).</param>
    /// <returns>For example "Waiting until 14:42 UTC to ask Amazon: it builds this report at most once every 30 minutes."</returns>
    public static string Describe(AmazonReportType reportType, DateTimeOffset waitUntil) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"Waiting until {waitUntil.UtcDateTime:HH:mm} UTC to ask Amazon: it builds this report at most once every {reportType.RequestCooldown().TotalMinutes:0} minutes.");
}
