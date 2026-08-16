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
    string Theme,
    // Default value keeps deserialization of settings files written before this field existed
    // backward-compatible: System.Text.Json falls back to a parameter's declared default for a
    // missing JSON property, rather than the value type's default(int).
    int DefaultTargetStockDays = 45)
{
    public static AppSettings CreateDefault() => new(
        ClientId: null,
        ClientSecret: null,
        RefreshToken: null,
        ApiHost: "https://sellingpartnerapi-na.amazon.com",
        MarketplaceId: "ATVPDKIKX0DER",
        Theme: "System",
        DefaultTargetStockDays: 45);
}
