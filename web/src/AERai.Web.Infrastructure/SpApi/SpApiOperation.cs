using System.Collections.Frozen;
using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.SpApi;

/// <summary>
/// Names and published rate limits of the SP-API operations the app calls. SP-API limits are per
/// operation (not global), so each gets its own token bucket in <see cref="SpApiRateLimiter"/>.
/// </summary>
internal static class SpApiOperation
{
    /// <summary>Request option carrying the operation name from a typed client to the pipeline handler.</summary>
    public static readonly HttpRequestOptionsKey<string> OptionKey = new("AERai.SpApiOperation");

    /// <summary>Request option carrying the SP-API region, which selects the access token and rate-limit buckets.</summary>
    public static readonly HttpRequestOptionsKey<AmazonRegion> RegionKey = new("AERai.SpApiRegion");

    public const string CreateReport = "reports.createReport";
    public const string GetReport = "reports.getReport";
    public const string GetReports = "reports.getReports";
    public const string GetReportDocument = "reports.getReportDocument";

    /// <summary>
    /// (requests per second, burst) from the Reports API v2021-06-30 usage plans. Unknown operations
    /// fall back to a conservative default.
    /// </summary>
    public static readonly FrozenDictionary<string, (double RatePerSecond, int Burst)> Limits =
        new Dictionary<string, (double, int)>(StringComparer.Ordinal)
        {
            [CreateReport] = (0.0167, 15),
            [GetReport] = (2.0, 15),
            [GetReports] = (0.0222, 10),
            [GetReportDocument] = (0.0167, 15),
        }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Limit applied to operations missing from <see cref="Limits"/>.</summary>
    public static readonly (double RatePerSecond, int Burst) DefaultLimit = (0.5, 1);
}
