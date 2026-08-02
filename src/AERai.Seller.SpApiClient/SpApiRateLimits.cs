namespace AERai.Seller.SpApiClient;

/// <summary>
/// Default per-operation rate limits, matching SP-API's published "standard" usage plans as of this
/// writing. Amazon can grant an application a different (higher) plan — verify against the response
/// headers / your Seller Central app's usage plan and adjust here if they don't match.
/// </summary>
public static class SpApiRateLimits
{
    public static readonly IReadOnlyDictionary<string, SpApiRateLimit> Defaults = new Dictionary<string, SpApiRateLimit>
    {
        ["Reports.CreateReport"] = new SpApiRateLimit(RequestsPerSecond: 0.0167, Burst: 15),
        ["Reports.GetReport"] = new SpApiRateLimit(RequestsPerSecond: 2.0, Burst: 15),
        ["Reports.GetReportDocument"] = new SpApiRateLimit(RequestsPerSecond: 0.0222, Burst: 10),
        ["Orders.SearchOrders"] = new SpApiRateLimit(RequestsPerSecond: 0.0056, Burst: 20),
    };
}
