using AERai.Web.Domain.Core;

namespace AERai.Web.Application.Tests;

/// <summary>The seeded marketplaces, for tests that need a <see cref="Marketplace"/>.</summary>
internal static class TestMarketplaces
{
    public static Marketplace UnitedStates => new()
    {
        MarketplaceId = MarketplaceIds.UnitedStates, Code = "US", Name = "United States", Region = AmazonRegion.NorthAmerica,
        Currency = "USD", TimeZoneId = "America/New_York", SalesChannel = "Amazon.com", SortOrder = 1, IsActive = true,
    };

    public static Marketplace Canada => new()
    {
        MarketplaceId = MarketplaceIds.Canada, Code = "CA", Name = "Canada", Region = AmazonRegion.NorthAmerica,
        Currency = "CAD", TimeZoneId = "America/Toronto", SalesChannel = "Amazon.ca", SortOrder = 2, IsActive = true,
    };

    public static Marketplace UnitedKingdom => new()
    {
        MarketplaceId = MarketplaceIds.UnitedKingdom, Code = "UK", Name = "United Kingdom", Region = AmazonRegion.Europe,
        Currency = "GBP", TimeZoneId = "Europe/London", SalesChannel = "Amazon.co.uk", SortOrder = 3, IsActive = false,
    };

    public static IReadOnlyList<Marketplace> All => [UnitedStates, Canada, UnitedKingdom];
}
