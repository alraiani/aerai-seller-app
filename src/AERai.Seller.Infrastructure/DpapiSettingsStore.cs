using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text.Json;
using AERai.Seller.Application.Abstractions;
using AERai.Seller.SpApiClient;

namespace AERai.Seller.Infrastructure;

/// <summary>
/// Persists app settings (including SP-API credentials) encrypted at rest via Windows DPAPI.
/// Implements both the Application-facing settings interface and SpApiClient's minimal
/// credential contract, so SpApiClient never needs to know how/where credentials live.
/// Windows-only by design — this app only ever runs on Windows (WPF).
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class DpapiSettingsStore(string? settingsFilePathOverride = null) : IAppSettingsStore, ICredentialStore
{
    private readonly string _settingsFilePath = settingsFilePathOverride ?? GetDefaultSettingsFilePath();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public static string GetDefaultSettingsFilePath()
    {
        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AERaiSellerApp");
        Directory.CreateDirectory(appDataDir);
        return Path.Combine(appDataDir, "settings.dat");
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return AppSettings.CreateDefault();
            }

            var encryptedBytes = await File.ReadAllBytesAsync(_settingsFilePath, cancellationToken);
            var jsonBytes = ProtectedData.Unprotect(encryptedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize<AppSettings>(jsonBytes) ?? AppSettings.CreateDefault();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            var jsonBytes = JsonSerializer.SerializeToUtf8Bytes(settings);
            var encryptedBytes = ProtectedData.Protect(jsonBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
            await File.WriteAllBytesAsync(_settingsFilePath, encryptedBytes, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    async Task<SpApiCredentials?> ICredentialStore.GetCredentialsAsync(CancellationToken cancellationToken)
    {
        var settings = await GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.ClientId)
            || string.IsNullOrWhiteSpace(settings.ClientSecret)
            || string.IsNullOrWhiteSpace(settings.RefreshToken))
        {
            return null;
        }

        return new SpApiCredentials(
            settings.ClientId, settings.ClientSecret, settings.RefreshToken, settings.ApiHost, settings.MarketplaceId);
    }
}
