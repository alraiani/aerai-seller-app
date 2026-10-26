namespace AERai.Web.Infrastructure.AmazonAds;

/// <summary>How the app connects to the Amazon Ads API.</summary>
public enum AmazonAdsMode
{
    /// <summary>No connection; Ads spend runs fail with a message saying so.</summary>
    Disabled = 0,

    /// <summary>Built-in simulator that makes up spend for the catalog's SKUs. Development only.</summary>
    Simulated = 1,

    /// <summary>Real Amazon Ads API calls with the configured credentials.</summary>
    Live = 2,
}
