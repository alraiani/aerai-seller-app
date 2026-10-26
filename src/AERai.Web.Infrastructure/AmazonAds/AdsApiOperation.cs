using AERai.Web.Domain.Core;

namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>Names of the Amazon Ads API operations the app calls, and the request options that tag them.</summary>
internal static class AdsApiOperation
{
    /// <summary>Request option carrying the operation name from the client to the pipeline handler (for logging).</summary>
    public static readonly HttpRequestOptionsKey<string> OptionKey = new("AERai.AdsOperation");

    /// <summary>Request option carrying the region, which selects the access token.</summary>
    public static readonly HttpRequestOptionsKey<AmazonRegion> RegionKey = new("AERai.AdsRegion");

    /// <summary>Request option carrying the advertising profile id, sent as <c>Amazon-Advertising-API-Scope</c>.</summary>
    public static readonly HttpRequestOptionsKey<string> ProfileKey = new("AERai.AdsProfile");

    public const string ListProfiles = "ads.listProfiles";
    public const string CreateReport = "ads.createReport";
    public const string GetReport = "ads.getReport";
}
