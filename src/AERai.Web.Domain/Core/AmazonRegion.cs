namespace AERai.Web.Domain.Core;

/// <summary>
/// An SP-API region. Each region has its own endpoint and seller authorization (refresh token),
/// so marketplaces in different regions cannot share credentials.
/// </summary>
public enum AmazonRegion
{
    /// <summary>North America (<c>sellingpartnerapi-na</c>): United States, Canada, Mexico, Brazil.</summary>
    NorthAmerica = 1,

    /// <summary>Europe (<c>sellingpartnerapi-eu</c>): United Kingdom, Germany, France, and others.</summary>
    Europe = 2,
}
