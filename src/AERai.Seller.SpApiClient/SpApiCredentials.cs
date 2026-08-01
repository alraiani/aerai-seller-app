namespace AERai.Seller.SpApiClient;

public sealed record SpApiCredentials(
    string ClientId,
    string ClientSecret,
    string RefreshToken,
    string ApiHost,
    string MarketplaceId);
