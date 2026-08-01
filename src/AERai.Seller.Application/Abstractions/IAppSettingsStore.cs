namespace AERai.Seller.Application.Abstractions;

public interface IAppSettingsStore
{
    Task<AppSettings> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed record AppSettings(
    string? ClientId,
    string? ClientSecret,
    string? RefreshToken,
    string ApiHost,
    string MarketplaceId,
    string Theme)
{
    public static AppSettings CreateDefault() => new(
        ClientId: null,
        ClientSecret: null,
        RefreshToken: null,
        ApiHost: "https://sellingpartnerapi-na.amazon.com",
        MarketplaceId: "ATVPDKIKX0DER",
        Theme: "System");
}
